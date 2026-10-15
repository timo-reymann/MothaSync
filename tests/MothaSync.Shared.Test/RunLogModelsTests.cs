using MothaSync.Shared;

namespace MothaSync.Shared.Test;

public sealed class RunLogModelsTests
{
    [Fact]
    public void SyncRunStatus_ValuesAreStable()
    {
        Assert.Equal(0, (int)SyncRunStatus.Running);
        Assert.Equal(1, (int)SyncRunStatus.Completed);
        Assert.Equal(2, (int)SyncRunStatus.Failed);
        Assert.Equal(3, (int)SyncRunStatus.Cancelled);
    }

    [Fact]
    public void SyncRunRecord_EqualValuesAreEqual()
    {
        var started = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var completed = started.AddMinutes(12);

        var a = new SyncRunRecord(1, started, completed, SyncRunStatus.Completed,
            "/src", "/dst", FilesProcessed: 42, BytesTransferred: 1024, ErrorCount: 0, LogText: "ok");
        var b = new SyncRunRecord(1, started, completed, SyncRunStatus.Completed,
            "/src", "/dst", FilesProcessed: 42, BytesTransferred: 1024, ErrorCount: 0, LogText: "ok");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void SyncRunRecord_DifferentValuesAreNotEqual()
    {
        var started = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var baseline = new SyncRunRecord(1, started, null, SyncRunStatus.Running,
            "/src", "/dst", 0, 0, 0, string.Empty);

        Assert.NotEqual(baseline with { Id = 2 }, baseline);
        Assert.NotEqual(baseline with { Status = SyncRunStatus.Failed }, baseline);
        Assert.NotEqual(baseline with { CompletedAtUtc = started }, baseline);
        Assert.NotEqual(baseline with { LogText = "different" }, baseline);
    }

    [Fact]
    public void SyncRunRecord_WithExpressionCopiesOnlyGivenProperty()
    {
        var started = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var running = new SyncRunRecord(7, started, null, SyncRunStatus.Running,
            "/src", "/dst", 0, 0, 0, string.Empty);

        var completed = running with
        {
            CompletedAtUtc = started.AddMinutes(3),
            Status = SyncRunStatus.Completed,
            FilesProcessed = 10,
            BytesTransferred = 2048,
        };

        Assert.Equal(7, completed.Id);
        Assert.Equal("/src", completed.SourcePath);
        Assert.Equal(SyncRunStatus.Completed, completed.Status);
        Assert.Equal(started.AddMinutes(3), completed.CompletedAtUtc);
        Assert.Equal(10, completed.FilesProcessed);
        Assert.Equal(2048, completed.BytesTransferred);
    }

    [Fact]
    public void SyncItemRecord_ErrorMessageDefaultsToNull()
    {
        var item = new SyncItemRecord(5, "docs/a.txt", 100, "copied");

        Assert.Null(item.ErrorMessage);
    }

    [Fact]
    public void SyncErrorRecord_PositionalEquality()
    {
        var createdAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var a = new SyncErrorRecord(9, "docs/b.txt", "permission denied", createdAt);
        var b = new SyncErrorRecord(9, "docs/b.txt", "permission denied", createdAt);

        Assert.Equal(a, b);
        Assert.Equal("permission denied", a.Message);
        Assert.Equal(createdAt, a.CreatedAtUtc);
    }
}
