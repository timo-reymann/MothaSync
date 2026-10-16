using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MothaSync.Shared;

namespace MothaSync.Client;

public sealed record SyncTick(
    string Timestamp,
    string FileName,
    string Message,
    double Percent,
    int LinesDone,
    int TotalLines)
{
    public double? EtaSeconds { get; init; }
    public IReadOnlyList<string> LogLines { get; init; } = [];
}

public static class RcloneSyncEngine
{
    private static readonly TimeSpan StatsInterval = TimeSpan.FromMilliseconds(500);
    private static CancellationTokenSource? _cts;

    public static bool IsRunning { get; private set; }
    public static bool IsPaused { get; private set; }

    public static event Action<SyncTick>? Tick;
    public static event Action? Completed;
    public static event Action? Failed;

    public static void Start()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        IsPaused = false;
        _cts = new CancellationTokenSource();
        var settings = SettingsStore.Load();
        var store = new RunLogStore();
        var runId = store.StartRun(settings.SourcePath, settings.DestinationPath);
        _ = RunAsync(_cts.Token, store, runId, settings);
    }

    public static void TogglePause()
    {
        if (IsRunning)
        {
            IsPaused = !IsPaused;
        }
    }

    private static async Task RunAsync(CancellationToken token, RunLogStore store, long runId, AppSettings settings)
    {
        var logLines = new List<string>();
        var loggedTransfers = new HashSet<string>();
        try
        {
            if (SyncPaths.AreSame(settings.SourcePath, settings.DestinationPath))
            {
                throw new InvalidOperationException($"Source and destination are the same folder: {settings.SourcePath.Trim()}");
            }

            await using var daemon = await RcloneDaemon.StartAsync(RcloneBootstrap.ExecutablePath, token);
            var startLines = new[]
            {
                LogLine("INFO", $"Starting sync from {settings.SourcePath} to {settings.DestinationPath}"),
                LogLine("INFO", "Starting rclone sync API")
            };
            logLines.AddRange(startLines);
            PostLogLines(startLines);

            var jobId = await daemon.Client.StartSyncAsync(settings.SourcePath, settings.DestinationPath, token);
            var jobLine = LogLine("INFO", $"rclone sync API job {jobId} started; polling transfer status");
            logLines.Add(jobLine);
            PostLogLines([jobLine]);

            while (true)
            {
                await Task.Delay(StatsInterval, token);
                var stats = await daemon.Client.GetStatsAsync(jobId, token);
                var transferred = await daemon.Client.GetTransferredAsync(jobId, token);
                PublishStats(stats, transferred, logLines, loggedTransfers);
                var status = await daemon.Client.GetJobStatusAsync(jobId, token);
                if (!status.Finished)
                {
                    continue;
                }

                if (!status.Success)
                {
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(status.Error) ? "rclone sync failed." : status.Error);
                }

                var finishLines = new[]
                {
                    LogLine("INFO", "Stopping rclone sync API"),
                    LogLine("INFO", "Sync finished with status completed")
                };
                logLines.AddRange(finishLines);
                PostLogLines(finishLines);
                var reported = await store.CompleteRunAsync(
                    runId, SyncRunStatus.Completed, stats.Transfers, stats.Bytes, string.Join('\n', logLines));
                var reportLine = reported
                    ? LogLine("INFO", "Server report accepted")
                    : LogLine("ERROR", "Server report failed; the configured server did not accept the run");
                PostLogLines([reportLine]);
                if (!reported)
                {
                    UserNotifications.ShowError("Server reporting failed", "The sync completed locally, but the configured server did not accept the run.");
                    PostFailed();
                    return;
                }
                PostCompleted();
                return;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            var cancelLines = new[]
            {
                LogLine("INFO", "Stopping rclone sync API"),
                LogLine("INFO", "Sync finished with status cancelled")
            };
            logLines.AddRange(cancelLines);
            PostLogLines(cancelLines);
            store.CompleteRun(runId, SyncRunStatus.Cancelled, 0, 0, string.Join('\n', logLines));
            UserNotifications.ShowError("Sync cancelled", "The rclone sync was cancelled before it finished.");
            PostStopped();
        }
        catch (Exception exception)
        {
            var failureLines = new[]
            {
                LogLine("INFO", "Stopping rclone sync API"),
                LogLine("ERROR", $"Sync finished with status failed: {exception.Message}")
            };
            logLines.AddRange(failureLines);
            PostLogLines(failureLines);
            store.AddError(new SyncErrorRecord(runId, string.Empty, exception.Message, DateTimeOffset.UtcNow));
            store.CompleteRun(runId, SyncRunStatus.Failed, 0, 0, string.Join('\n', logLines));
            UserNotifications.ShowError("Sync failed", exception.Message);
            PostFailed();
        }
    }

    private static void PublishStats(
        RcloneStats stats,
        IReadOnlyList<RcloneCompletedTransfer> transferred,
        List<string> logLines,
        HashSet<string> loggedTransfers)
    {
        var newLogLines = new List<string>();
        foreach (var transfer in transferred)
        {
            // core/transferred also contains rclone's comparison/check records.
            // They are not file transfers and can appear before an initial copy.
            if (transfer.Checked)
            {
                continue;
            }

            var key = $"{transfer.Timestamp}:{transfer.Name}";
            if (!loggedTransfers.Add(key))
            {
                continue;
            }

            var level = string.IsNullOrWhiteSpace(transfer.Error) ? "INFO" : "ERROR";
            var transferMessage = string.IsNullOrWhiteSpace(transfer.Error)
                ? $"{level}  Copied: {transfer.Name} ({FormatBytes(transfer.Bytes)} / {FormatBytes(transfer.Size)})"
                : $"{level}  Transfer failed: {transfer.Name}: {transfer.Error}";
            var logLine = $"{DateTime.Now:HH:mm:ss} {transferMessage}";
            logLines.Add(logLine);
            newLogLines.Add(logLine);
            if (!string.IsNullOrWhiteSpace(transfer.Error))
            {
                UserNotifications.ShowError(
                    "File transfer failed",
                    $"{transfer.Name}: {transfer.Error}");
            }
        }

        var total = stats.TotalBytes > 0 ? stats.TotalBytes : stats.TotalTransfers;
        var completed = stats.TotalBytes > 0 ? stats.Bytes : stats.Transfers;
        var percent = total > 0 ? Math.Min(100, completed * 100d / total) : 0;
        var active = stats.Transferring.FirstOrDefault();
        var fileName = active?.Name ?? transferred.LastOrDefault()?.Name ?? "rclone sync";
        var message = active is null
            ? $"INFO  Transferred {stats.Transfers} files ({FormatBytes(stats.Bytes)})"
            : $"INFO  Transferring: {active.Name} ({active.Percentage}%, {FormatBytes(active.Bytes)})";
        var tick = new SyncTick(
            DateTime.Now.ToString("HH:mm:ss"),
            fileName,
            message,
            percent,
            stats.Transfers,
            Math.Max(stats.TotalTransfers, stats.Transfers))
        {
            EtaSeconds = active?.Eta,
            LogLines = newLogLines
        };
        Dispatcher.UIThread.Post(() => Tick?.Invoke(tick));
    }

    private static void PostLogLines(IReadOnlyList<string> lines) => Dispatcher.UIThread.Post(() =>
        Tick?.Invoke(new SyncTick("", "", "", 0, 0, 0) { LogLines = lines }));

    private static string LogLine(string level, string message) =>
        $"{DateTime.Now:HH:mm:ss} {level}  {message}";

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):F1} GiB",
        >= 1024 * 1024 => $"{bytes / (1024d * 1024):F1} MiB",
        >= 1024 => $"{bytes / 1024d:F1} KiB",
        _ => $"{bytes} B"
    };

    private static void PostCompleted() => Dispatcher.UIThread.Post(() =>
    {
        LastSyncStore.MarkCompleted();
        IsRunning = false;
        IsPaused = false;
        Completed?.Invoke();
    });

    private static void PostFailed() => Dispatcher.UIThread.Post(() =>
    {
        IsRunning = false;
        IsPaused = false;
        Failed?.Invoke();
    });

    private static void PostStopped() => Dispatcher.UIThread.Post(() =>
    {
        IsRunning = false;
        IsPaused = false;
    });
}
