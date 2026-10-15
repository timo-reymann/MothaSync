using Microsoft.Data.Sqlite;
using MothaSync.Shared;

namespace MothaSync.Client.Test;

public sealed class RunLogStoreTests
{
    [Fact]
    public void StartRun_CreatesRunningRunVisibleInHistory()
    {
        using var db = new TempHistoryDb();

        var runId = db.Store.StartRun("/mnt/source", "/mnt/backup");

        var run = Assert.Single(db.Store.GetRecentRuns());
        Assert.Equal(runId, run.Id);
        Assert.Equal(SyncRunStatus.Running, run.Status);
        Assert.Null(run.CompletedAtUtc);
        Assert.Equal("/mnt/source", run.SourcePath);
        Assert.Equal("/mnt/backup", run.DestinationPath);
    }

    [Fact]
    public void CompleteRun_UpdatesStatusCountsAndLogText()
    {
        using var db = new TempHistoryDb();
        var runId = db.Store.StartRun("/mnt/source", "/mnt/backup");

        db.Store.CompleteRun(runId, SyncRunStatus.Completed,
            filesProcessed: 100, bytesTransferred: 419_430_400,
            logText: "line 1\nline 2");

        var run = Assert.Single(db.Store.GetRecentRuns());
        Assert.Equal(SyncRunStatus.Completed, run.Status);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.Equal(100, run.FilesProcessed);
        Assert.Equal(419_430_400, run.BytesTransferred);
        Assert.Equal(0, run.ErrorCount);
        Assert.Equal("line 1\nline 2", run.LogText);
    }

    [Fact]
    public void CompleteRun_CancelledRun_IsRecordedAsCancelled()
    {
        using var db = new TempHistoryDb();
        var runId = db.Store.StartRun("/mnt/source", "/mnt/backup");

        db.Store.CompleteRun(runId, SyncRunStatus.Cancelled,
            filesProcessed: 3, bytesTransferred: 12_582_912, logText: "partial");

        var run = Assert.Single(db.Store.GetRecentRuns());
        Assert.Equal(SyncRunStatus.Cancelled, run.Status);
        Assert.Equal(3, run.FilesProcessed);
        Assert.Equal("partial", run.LogText);
    }

    [Fact]
    public void AddError_CountedInErrorCountOnComplete()
    {
        using var db = new TempHistoryDb();
        var runId = db.Store.StartRun("/mnt/source", "/mnt/backup");
        var at = DateTimeOffset.UtcNow;
        db.Store.AddError(new SyncErrorRecord(runId, "docs/a.txt", "permission denied", at));
        db.Store.AddError(new SyncErrorRecord(runId, "docs/b.txt", "disk full", at));

        db.Store.CompleteRun(runId, SyncRunStatus.Failed,
            filesProcessed: 2, bytesTransferred: 0, logText: "failed");

        var run = Assert.Single(db.Store.GetRecentRuns());
        Assert.Equal(SyncRunStatus.Failed, run.Status);
        Assert.Equal(2, run.ErrorCount);
    }

    [Fact]
    public void AddItem_PersistsItemRowsForTheRun()
    {
        using var db = new TempHistoryDb();
        var runId = db.Store.StartRun("/mnt/source", "/mnt/backup");
        db.Store.AddItem(new SyncItemRecord(runId, "chunk_01.dat", 4_194_304, "transferred"));
        db.Store.AddItem(new SyncItemRecord(runId, "chunk_02.dat", 4_194_304, "transferred", ErrorMessage: null));

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = db.Path }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RelativePath, Size, Status FROM SyncItems WHERE RunId = $runId ORDER BY Id;";
        command.Parameters.AddWithValue("$runId", runId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("chunk_01.dat", reader.GetString(0));
        Assert.Equal(4_194_304, reader.GetInt64(1));
        Assert.Equal("transferred", reader.GetString(2));
        Assert.True(reader.Read());
        Assert.Equal("chunk_02.dat", reader.GetString(0));
        Assert.False(reader.Read());
    }

    [Fact]
    public void GetRecentRuns_ReturnsNewestFirst_AndHonoursLimit()
    {
        using var db = new TempHistoryDb();
        var first = db.Store.StartRun("/a", "/b");
        var second = db.Store.StartRun("/a", "/b");
        db.Store.CompleteRun(first, SyncRunStatus.Completed, 1, 1, "");
        db.Store.CompleteRun(second, SyncRunStatus.Completed, 1, 1, "");

        var runs = db.Store.GetRecentRuns(limit: 1);

        Assert.Equal(second, Assert.Single(runs).Id);
    }

    [Fact]
    public void CompleteRun_TrimsHistoryToNewest100Runs()
    {
        using var db = new TempHistoryDb();
        for (var i = 0; i < 105; i++)
        {
            var runId = db.Store.StartRun("/a", "/b");
            db.Store.CompleteRun(runId, SyncRunStatus.Completed, 1, 1, "");
        }

        var runs = db.Store.GetRecentRuns(limit: 100);

        Assert.Equal(100, runs.Count);
        // The oldest five runs were trimmed.
        Assert.DoesNotContain(runs, r => r.Id == 1);
    }
}
