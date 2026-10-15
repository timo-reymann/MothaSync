using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using MothaSync.Shared.Contracts;
using ProtoSyncRunStatus = MothaSync.Shared.Contracts.SyncRunStatus;

namespace MothaSync.Shared.Test;

public sealed class ProtoContractTests
{
    [Fact]
    public void SyncRunStatus_UnspecifiedIsZero()
    {
        // The server treats status 0 as "all statuses" in queries and rejects it on
        // report; Unspecified must stay the proto3 zero value.
        Assert.Equal(0, (int)ProtoSyncRunStatus.Unspecified);
        Assert.Equal(1, (int)ProtoSyncRunStatus.Running);
        Assert.Equal(2, (int)ProtoSyncRunStatus.Completed);
        Assert.Equal(3, (int)ProtoSyncRunStatus.Failed);
        Assert.Equal(4, (int)ProtoSyncRunStatus.Cancelled);
    }

    [Fact]
    public void SyncRun_Defaults()
    {
        var run = new SyncRun();

        Assert.Equal(string.Empty, run.Id);
        Assert.Equal(ProtoSyncRunStatus.Unspecified, run.Status);
        Assert.Null(run.StartedAt);
        Assert.Null(run.Duration);
        Assert.Equal(0, run.FilesChanged);
        Assert.Equal(0, run.BytesTransferred);
        Assert.Equal(string.Empty, run.ErrorMessage);
        Assert.Equal(string.Empty, run.DeviceId);
    }

    [Fact]
    public void RegisterDeviceRequest_RoundTripsThroughBinary()
    {
        var request = new RegisterDeviceRequest
        {
            EnrollmentToken = "secret-token",
            DeviceName = "laptop",
            Platform = "linux-x64",
            ClientVersion = "1.0.0",
        };

        var parsed = RegisterDeviceRequest.Parser.ParseFrom(request.ToByteArray());

        Assert.Equal(request, parsed);
        Assert.Equal("secret-token", parsed.EnrollmentToken);
        Assert.Equal("laptop", parsed.DeviceName);
        Assert.Equal("linux-x64", parsed.Platform);
        Assert.Equal("1.0.0", parsed.ClientVersion);
    }

    [Fact]
    public void RegisterDeviceResponse_RoundTripsThroughBinary()
    {
        var response = new RegisterDeviceResponse
        {
            DeviceId = Guid.NewGuid().ToString(),
            DeviceToken = "abc123",
        };

        var parsed = RegisterDeviceResponse.Parser.ParseFrom(response.ToByteArray());

        Assert.Equal(response, parsed);
    }

    [Fact]
    public void SyncRun_RoundTripsThroughBinary_WithTimestampAndDuration()
    {
        var started = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero);
        var run = new SyncRun
        {
            Id = Guid.NewGuid().ToString(),
            Status = ProtoSyncRunStatus.Failed,
            StartedAt = Timestamp.FromDateTimeOffset(started),
            Duration = Duration.FromTimeSpan(TimeSpan.FromSeconds(90)),
            FilesChanged = 12,
            BytesTransferred = 34567,
            ErrorMessage = "disk full",
            DeviceId = Guid.NewGuid().ToString(),
        };

        var parsed = SyncRun.Parser.ParseFrom(run.ToByteArray());

        Assert.Equal(run, parsed);
        Assert.Equal(started, parsed.StartedAt.ToDateTimeOffset());
        Assert.Equal(TimeSpan.FromSeconds(90), parsed.Duration.ToTimeSpan());
        Assert.Equal("disk full", parsed.ErrorMessage);
    }

    [Fact]
    public void ListSyncRunsResponse_RoundTripsRepeatedRunsAndPageToken()
    {
        var response = new ListSyncRunsResponse { NextPageToken = "50" };
        response.Runs.Add(new SyncRun { Id = Guid.NewGuid().ToString(), Status = ProtoSyncRunStatus.Completed });
        response.Runs.Add(new SyncRun { Id = Guid.NewGuid().ToString(), Status = ProtoSyncRunStatus.Running });

        var parsed = ListSyncRunsResponse.Parser.ParseFrom(response.ToByteArray());

        Assert.Equal("50", parsed.NextPageToken);
        Assert.Equal(2, parsed.Runs.Count);
        Assert.Equal(response.Runs, parsed.Runs);
    }

    [Fact]
    public void ReportSyncRunRequest_EmptyResponseMessagesRoundTrip()
    {
        var request = new ReportSyncRunRequest { Run = new SyncRun { Id = Guid.NewGuid().ToString() } };
        Assert.Equal(request, ReportSyncRunRequest.Parser.ParseFrom(request.ToByteArray()));

        var reportResponse = new ReportSyncRunResponse();
        Assert.Equal(reportResponse, ReportSyncRunResponse.Parser.ParseFrom(reportResponse.ToByteArray()));
    }
}
