using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;

namespace MothaSync.Client.Views;

public partial class DoneView : UserControl
{
    public DoneView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncStatusStore.Set(SyncStatus.Ok);
        var run = new RunLogStore().GetRecentRuns(1).FirstOrDefault();
        if (run is null)
        {
            return;
        }

        FilesText.Text = $"{run.FilesProcessed} files";
        SizeText.Text = FormatBytes(run.BytesTransferred);
        DurationText.Text = FormatDuration(run.CompletedAtUtc - run.StartedAtUtc);
    }

    private void OnViewHistory(object? sender, RoutedEventArgs e)
    {
        Navigation.ShowHistory(this);
    }

    private void OnBackToOverview(object? sender, RoutedEventArgs e)
    {
        Navigation.ShowOverview(this);
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):F1} GiB",
        >= 1024 * 1024 => $"{bytes / (1024d * 1024):F1} MiB",
        _ => $"{bytes / 1024d:F1} KiB"
    };

    private static string FormatDuration(TimeSpan? duration) => duration is { } value
        ? $"{value.Minutes}m {value.Seconds}s"
        : "duration unavailable";
}
