namespace MothaSync.Shared;

public enum SyncRunStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record SyncRunRecord(
    long Id,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    SyncRunStatus Status,
    string SourcePath,
    string DestinationPath,
    long FilesProcessed,
    long BytesTransferred,
    int ErrorCount,
    string LogText);

public sealed record SyncItemRecord(
    long RunId,
    string RelativePath,
    long Size,
    string Status,
    string? ErrorMessage = null);

public sealed record SyncErrorRecord(
    long RunId,
    string RelativePath,
    string Message,
    DateTimeOffset CreatedAtUtc);