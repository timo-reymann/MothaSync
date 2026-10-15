using System.Security.Cryptography;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using MothaSync.Server.Data;
using MothaSync.Shared.Contracts;
using ProtoSyncRunStatus = MothaSync.Shared.Contracts.SyncRunStatus;

namespace MothaSync.Server.Test;

public sealed class SyncDbTests : IDisposable
{
    private readonly TempDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static SyncRun NewRun(
        string? id = null,
        int minutesAgo = 0,
        ProtoSyncRunStatus status = ProtoSyncRunStatus.Completed,
        long filesChanged = 0,
        long bytesTransferred = 0,
        string errorMessage = "",
        TimeSpan? duration = null)
    {
        var run = new SyncRun
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Status = status,
            StartedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddMinutes(-minutesAgo)),
            FilesChanged = filesChanged,
            BytesTransferred = bytesTransferred,
            ErrorMessage = errorMessage,
        };
        if (duration is { } value)
        {
            run.Duration = Duration.FromTimeSpan(value);
        }

        return run;
    }

    private static string RegisterTestDevice(SyncDb db, string name = "device")
    {
        var token = SyncDb.NewToken();
        return db.RegisterDevice(new RegisterDeviceRequest { DeviceName = name }, SyncDb.HashToken(token));
    }

    [Fact]
    public void HashToken_ReturnsUppercaseSha256Hex()
    {
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Empty)));

        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", expected);
        Assert.Equal(expected, SyncDb.HashToken(string.Empty));
    }

    [Fact]
    public void HashToken_IsDeterministicAndDistinctPerToken()
    {
        Assert.Equal(SyncDb.HashToken("token-a"), SyncDb.HashToken("token-a"));
        Assert.NotEqual(SyncDb.HashToken("token-a"), SyncDb.HashToken("token-b"));
        Assert.Equal(64, SyncDb.HashToken("token-a").Length);
    }

    [Fact]
    public void NewToken_ReturnsLowercaseHexAndDiffersEachCall()
    {
        var first = SyncDb.NewToken();
        var second = SyncDb.NewToken();

        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9a-f]{64}$", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RegisterDevice_ThenFindDeviceByToken_ReturnsDeviceId()
    {
        var token = SyncDb.NewToken();
        var deviceId = _db.Db.RegisterDevice(
            new RegisterDeviceRequest { DeviceName = "laptop", Platform = "linux", ClientVersion = "1.0.0" },
            SyncDb.HashToken(token));

        Assert.NotEmpty(deviceId);
        Assert.Equal(deviceId, _db.Db.FindDeviceByToken(token));
    }

    [Fact]
    public void FindDeviceByToken_UnknownToken_ReturnsNull()
    {
        Assert.Null(_db.Db.FindDeviceByToken("never-registered"));
    }

    [Fact]
    public void UpsertRun_NewRun_ReturnsTrue()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        var run = NewRun(filesChanged: 3, bytesTransferred: 999);

        Assert.True(_db.Db.UpsertRun(deviceId, run));

        var (runs, hasMore) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);
        Assert.False(hasMore);
        var stored = Assert.Single(runs);
        Assert.Equal(run.Id, stored.Id);
        Assert.Equal(3, stored.FilesChanged);
        Assert.Equal(999, stored.BytesTransferred);
    }

    [Fact]
    public void UpsertRun_SameDevice_UpdatesExistingRun()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        var runId = Guid.NewGuid().ToString();
        Assert.True(_db.Db.UpsertRun(deviceId, NewRun(runId, status: ProtoSyncRunStatus.Running)));

        var update = NewRun(runId, status: ProtoSyncRunStatus.Failed, filesChanged: 7,
            errorMessage: "disk full", duration: TimeSpan.FromSeconds(2));
        Assert.True(_db.Db.UpsertRun(deviceId, update));

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);
        var stored = Assert.Single(runs);
        Assert.Equal(ProtoSyncRunStatus.Failed, stored.Status);
        Assert.Equal(7, stored.FilesChanged);
        Assert.Equal("disk full", stored.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(2), stored.Duration!.ToTimeSpan());
    }

    [Fact]
    public void UpsertRun_RunOwnedByAnotherDevice_ReturnsFalse()
    {
        var owner = RegisterTestDevice(_db.Db, "owner");
        var other = RegisterTestDevice(_db.Db, "other");
        var run = NewRun();

        Assert.True(_db.Db.UpsertRun(owner, run));
        Assert.False(_db.Db.UpsertRun(other, run));

        var (ownerRuns, _) = _db.Db.ListRuns(owner, ProtoSyncRunStatus.Unspecified, 0, 10);
        Assert.Single(ownerRuns);
        var (otherRuns, _) = _db.Db.ListRuns(other, ProtoSyncRunStatus.Unspecified, 0, 10);
        Assert.Empty(otherRuns);
    }

    [Fact]
    public void ListRuns_EmptyDatabase_ReturnsNoRuns()
    {
        var deviceId = RegisterTestDevice(_db.Db);

        var (runs, hasMore) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 50);

        Assert.Empty(runs);
        Assert.False(hasMore);
    }

    [Fact]
    public void ListRuns_OrdersByStartedAtDescending()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        var oldest = NewRun(minutesAgo: 30);
        var newest = NewRun(minutesAgo: 1);
        var middle = NewRun(minutesAgo: 10);
        _db.Db.UpsertRun(deviceId, oldest);
        _db.Db.UpsertRun(deviceId, newest);
        _db.Db.UpsertRun(deviceId, middle);

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);

        Assert.Equal(new[] { newest.Id, middle.Id, oldest.Id }, runs.Select(r => r.Id));
    }

    [Fact]
    public void ListRuns_StatusFilter_ReturnsOnlyMatchingRuns()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Completed, minutesAgo: 1));
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Failed, minutesAgo: 2));
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Completed, minutesAgo: 3));

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Completed, 0, 10);

        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal(ProtoSyncRunStatus.Completed, r.Status));
    }

    [Fact]
    public void ListRuns_UnspecifiedStatus_ReturnsAllStatuses()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Running, minutesAgo: 1));
        _db.Db.UpsertRun(deviceId, NewRun(status: ProtoSyncRunStatus.Cancelled, minutesAgo: 2));

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);

        Assert.Equal(2, runs.Count);
    }

    [Fact]
    public void ListRuns_PaginatesWithHasMoreFlag()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        var first = NewRun(minutesAgo: 1);
        var second = NewRun(minutesAgo: 2);
        var third = NewRun(minutesAgo: 3);
        _db.Db.UpsertRun(deviceId, first);
        _db.Db.UpsertRun(deviceId, second);
        _db.Db.UpsertRun(deviceId, third);

        var (page1, hasMore1) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 2);
        Assert.True(hasMore1);
        Assert.Equal(new[] { first.Id, second.Id }, page1.Select(r => r.Id));

        var (page2, hasMore2) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 2, 2);
        Assert.False(hasMore2);
        Assert.Equal(third.Id, Assert.Single(page2).Id);
    }

    [Fact]
    public void ListRuns_OnlyReturnsRunsOfRequestedDevice()
    {
        var deviceA = RegisterTestDevice(_db.Db, "a");
        var deviceB = RegisterTestDevice(_db.Db, "b");
        var runA = NewRun();
        var runB = NewRun();
        _db.Db.UpsertRun(deviceA, runA);
        _db.Db.UpsertRun(deviceB, runB);

        var (runs, _) = _db.Db.ListRuns(deviceA, ProtoSyncRunStatus.Unspecified, 0, 10);

        var stored = Assert.Single(runs);
        Assert.Equal(runA.Id, stored.Id);
        Assert.Equal(deviceA, stored.DeviceId);
    }

    [Fact]
    public void ListRuns_NullDurationAndEmptyErrorMessageRoundTrip()
    {
        var deviceId = RegisterTestDevice(_db.Db);
        _db.Db.UpsertRun(deviceId, NewRun());

        var (runs, _) = _db.Db.ListRuns(deviceId, ProtoSyncRunStatus.Unspecified, 0, 10);

        var stored = Assert.Single(runs);
        Assert.Null(stored.Duration);
        Assert.Equal(string.Empty, stored.ErrorMessage);
    }
}
