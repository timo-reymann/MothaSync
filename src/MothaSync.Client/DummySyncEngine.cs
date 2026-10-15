using System;
using System.Collections.Generic;
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
    int TotalLines);

public static class DummySyncEngine
{
    private const int DurationMs = 10_000;
    private const int TickMs = 100;
    private const int TotalLines = DurationMs / TickMs;
    private const long ChunkSizeBytes = 4 * 1024 * 1024;

    private static CancellationTokenSource? _cts;

    public static bool IsRunning { get; private set; }
    public static bool IsPaused { get; private set; }

    public static event Action<SyncTick>? Tick;
    public static event Action? Completed;

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
        _ = RunAsync(_cts.Token, store, runId);
    }

    public static void TogglePause()
    {
        if (!IsRunning)
        {
            return;
        }

        IsPaused = !IsPaused;
    }

    private static async Task RunAsync(CancellationToken token, RunLogStore store, long runId)
    {
        var logLines = new List<string>();
        long filesTransferred = 0;
        long bytesTransferred = 0;

        for (var i = 1; i <= TotalLines; i++)
        {
            try
            {
                while (IsPaused)
                {
                    await Task.Delay(TickMs, token);
                }

                await Task.Delay(TickMs, token);
            }
            catch (TaskCanceledException)
            {
                store.CompleteRun(runId, SyncRunStatus.Cancelled, filesTransferred, bytesTransferred,
                    string.Join('\n', logLines));
                IsRunning = false;
                return;
            }

            var fileName = $"chunk_{i:D2}.dat";
            var tick = new SyncTick(
                DateTime.Now.ToString("HH:mm:ss"),
                fileName,
                $"INFO  Transferred: {fileName}",
                i * 100.0 / TotalLines,
                i,
                TotalLines);

            logLines.Add($"{tick.Timestamp} {tick.Message}");
            store.AddItem(new SyncItemRecord(runId, fileName, ChunkSizeBytes, "transferred"));
            filesTransferred++;
            bytesTransferred += ChunkSizeBytes;

            Dispatcher.UIThread.Post(() => Tick?.Invoke(tick));
        }

        store.CompleteRun(runId, SyncRunStatus.Completed, filesTransferred, bytesTransferred,
            string.Join('\n', logLines));

        Dispatcher.UIThread.Post(() =>
        {
            LastSyncStore.MarkCompleted();
            IsRunning = false;
            IsPaused = false;
            Completed?.Invoke();
        });
    }
}
