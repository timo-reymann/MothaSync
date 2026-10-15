using System.Security.Claims;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MothaSync.Server.Auth;
using MothaSync.Server.Controllers;
using MothaSync.Server.Data;
using MothaSync.Shared.Contracts;
using ProtoSyncRunStatus = MothaSync.Shared.Contracts.SyncRunStatus;

namespace MothaSync.Server.Test;

public sealed class RunsControllerTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private RunsController CreateController(string deviceId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(DeviceAuthentication.DeviceIdClaim, deviceId)],
            "Test");

        return new RunsController(_db.Db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    private string RegisterDevice(string name = "device")
    {
        var token = SyncDb.NewToken();
        return _db.Db.RegisterDevice(new RegisterDeviceRequest { DeviceName = name }, SyncDb.HashToken(token));
    }

    private static SyncRun NewRun(
        string? id = null,
        int minutesAgo = 0,
        ProtoSyncRunStatus status = ProtoSyncRunStatus.Completed,
        long filesChanged = 0,
        long bytesTransferred = 0)
    {
        return new SyncRun
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Status = status,
            StartedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)),
            FilesChanged = filesChanged,
            BytesTransferred = bytesTransferred,
        };
    }

    [Fact]
    public void Report_MissingRun_ReturnsBadRequest()
    {
        var controller = CreateController(RegisterDevice());

        var result = controller.Report(new ReportSyncRunRequest());

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Report_NonUuidRunId_ReturnsBadRequest()
    {
        var controller = CreateController(RegisterDevice());
        var request = new ReportSyncRunRequest { Run = NewRun(id: "not-a-guid") };

        var result = controller.Report(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Report_MissingStartedAt_ReturnsBadRequest()
    {
        var controller = CreateController(RegisterDevice());
        var run = NewRun();
        run.StartedAt = null;
        var request = new ReportSyncRunRequest { Run = run };

        var result = controller.Report(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Report_UnspecifiedStatus_ReturnsBadRequest()
    {
        var controller = CreateController(RegisterDevice());
        var request = new ReportSyncRunRequest { Run = NewRun(status: ProtoSyncRunStatus.Unspecified) };

        var result = controller.Report(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void Report_ValidRun_ReturnsOkAndStoresIt()
    {
        var deviceId = RegisterDevice();
        var controller = CreateController(deviceId);
        var run = NewRun(filesChanged: 5, bytesTransferred: 1000);

        var result = controller.Report(new ReportSyncRunRequest { Run = run });

        Assert.Null(result.Result);
        Assert.NotNull(result.Value);

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);
        var stored = Assert.Single(runs);
        Assert.Equal(run.Id, stored.Id);
        Assert.Equal(deviceId, stored.DeviceId);
    }

    [Fact]
    public void Report_RunOwnedByOtherDevice_ReturnsConflict()
    {
        var owner = CreateController(RegisterDevice("owner"));
        var other = CreateController(RegisterDevice("other"));
        var run = NewRun();
        Assert.Null(owner.Report(new ReportSyncRunRequest { Run = run }).Result);

        var result = other.Report(new ReportSyncRunRequest { Run = run });

        Assert.IsType<ConflictResult>(result.Result);
    }

    [Fact]
    public void Report_UpdatingOwnRun_Succeeds()
    {
        var deviceId = RegisterDevice();
        var controller = CreateController(deviceId);
        var runId = Guid.NewGuid().ToString();
        var running = NewRun(runId, status: ProtoSyncRunStatus.Running);
        var completed = NewRun(runId, status: ProtoSyncRunStatus.Completed, filesChanged: 9);

        Assert.Null(controller.Report(new ReportSyncRunRequest { Run = running }).Result);
        Assert.Null(controller.Report(new ReportSyncRunRequest { Run = completed }).Result);

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);
        var stored = Assert.Single(runs);
        Assert.Equal(ProtoSyncRunStatus.Completed, stored.Status);
        Assert.Equal(9, stored.FilesChanged);
    }

    [Fact]
    public void List_EmptyDatabase_ReturnsEmptyResponseWithoutPageToken()
    {
        var controller = CreateController(RegisterDevice());

        var result = controller.List(new ListSyncRunsRequest());

        Assert.Null(result.Result);
        var response = result.Value;
        Assert.NotNull(response);
        Assert.Empty(response.Runs);
        Assert.Equal(string.Empty, response.NextPageToken);
    }

    [Fact]
    public void List_InvalidPageToken_ReturnsBadRequest()
    {
        var controller = CreateController(RegisterDevice());

        foreach (var token in new[] { "abc", "-1", "1.5", " " })
        {
            var result = controller.List(new ListSyncRunsRequest { PageToken = token });
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }

    [Fact]
    public void List_NonPositivePageSize_UsesDefaultOf50()
    {
        var deviceId = RegisterDevice();
        var controller = CreateController(deviceId);
        for (var i = 0; i < 51; i++)
        {
            Assert.True(_db.Db.UpsertRun(deviceId, NewRun(minutesAgo: i)));
        }

        var result = controller.List(new ListSyncRunsRequest { PageSize = 0 });

        var response = result.Value;
        Assert.NotNull(response);
        Assert.Equal(50, response.Runs.Count);
        Assert.Equal("50", response.NextPageToken);
    }

    [Fact]
    public void List_PageSizeAboveMax_ClampsTo200()
    {
        var deviceId = RegisterDevice();
        var controller = CreateController(deviceId);
        for (var i = 0; i < 201; i++)
        {
            Assert.True(_db.Db.UpsertRun(deviceId, NewRun(minutesAgo: i)));
        }

        var result = controller.List(new ListSyncRunsRequest { PageSize = 500 });

        var response = result.Value;
        Assert.NotNull(response);
        Assert.Equal(200, response.Runs.Count);
        Assert.Equal("200", response.NextPageToken);
    }

    [Fact]
    public void List_PaginatesUsingNextPageToken()
    {
        var deviceId = RegisterDevice();
        var controller = CreateController(deviceId);
        var expected = Enumerable.Range(0, 5).Select(i => NewRun(minutesAgo: i)).ToArray();
        foreach (var run in expected)
        {
            Assert.True(_db.Db.UpsertRun(deviceId, run));
        }

        var page1 = controller.List(new ListSyncRunsRequest { PageSize = 2 }).Value;
        Assert.NotNull(page1);
        Assert.Equal(expected.Take(2).Select(r => r.Id), page1.Runs.Select(r => r.Id));
        Assert.Equal("2", page1.NextPageToken);

        var page2 = controller.List(new ListSyncRunsRequest { PageSize = 2, PageToken = page1.NextPageToken }).Value;
        Assert.NotNull(page2);
        Assert.Equal(expected.Skip(2).Take(2).Select(r => r.Id), page2.Runs.Select(r => r.Id));
        Assert.Equal("4", page2.NextPageToken);

        var page3 = controller.List(new ListSyncRunsRequest { PageSize = 2, PageToken = page2.NextPageToken }).Value;
        Assert.NotNull(page3);
        Assert.Equal(expected[4].Id, Assert.Single(page3.Runs).Id);
        Assert.Equal(string.Empty, page3.NextPageToken);
    }

    [Fact]
    public void List_StatusFilter_ReturnsOnlyMatchingRuns()
    {
        var deviceId = RegisterDevice();
        var controller = CreateController(deviceId);
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Completed, minutesAgo: 1));
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Failed, minutesAgo: 2));

        var result = controller.List(new ListSyncRunsRequest { Status = ProtoSyncRunStatus.Failed });

        var response = result.Value;
        Assert.NotNull(response);
        var run = Assert.Single(response.Runs);
        Assert.Equal(ProtoSyncRunStatus.Failed, run.Status);
    }

    [Fact]
    public void List_DoesNotReturnRunsOfOtherDevices()
    {
        var deviceA = RegisterDevice("a");
        var deviceB = RegisterDevice("b");
        var controllerA = CreateController(deviceA);
        var runForB = NewRun();
        Assert.Null(CreateController(deviceB).Report(new ReportSyncRunRequest { Run = runForB }).Result);

        var result = controllerA.List(new ListSyncRunsRequest());

        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Runs);
    }
}
