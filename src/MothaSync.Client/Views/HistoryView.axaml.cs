using System;
using System.Globalization;
using Avalonia.Controls;
using MothaSync.Shared;

namespace MothaSync.Client.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        LoadRuns();
    }

    private void LoadRuns()
    {
        var runs = new RunLogStore().GetRecentRuns();
        foreach (var run in runs)
        {
            RunList.Children.Add(new HistoryRow
            {
                RunId = run.Id,
                Date = run.StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                Files = $"{run.FilesProcessed:N0} files",
                Size = FormatBytes(run.BytesTransferred),
                Duration = FormatDuration(run.StartedAtUtc, run.CompletedAtUtc),
                Failed = run.Status is SyncRunStatus.Failed or SyncRunStatus.Cancelled,
                LogText = run.LogText
            });
        }

        EmptyState.IsVisible = runs.Count == 0;
    }

    private static string FormatBytes(long bytes)
    {
        const double unit = 1024;
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / unit:0.#} KB",
            < 1024 * 1024 * 1024 => $"{bytes / unit / unit:0.#} MB",
            _ => $"{bytes / unit / unit / unit:0.#} GB"
        };
    }

    private static string FormatDuration(DateTimeOffset startedAt, DateTimeOffset? completedAt)
    {
        if (completedAt is null)
        {
            return "running";
        }

        var duration = completedAt.Value - startedAt;
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{duration.Minutes}m {duration.Seconds:D2}s";
    }
}
