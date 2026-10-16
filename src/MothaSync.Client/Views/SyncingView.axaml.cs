using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MothaSync.Client.Views;

public partial class SyncingView : UserControl
{
    public SyncingView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncStatusStore.Set(SyncStatus.Running);
        DummySyncEngine.Tick += OnTick;
        DummySyncEngine.Completed += OnCompleted;
        DummySyncEngine.Failed += OnFailed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DummySyncEngine.Tick -= OnTick;
        DummySyncEngine.Completed -= OnCompleted;
        DummySyncEngine.Failed -= OnFailed;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTick(SyncTick tick)
    {
        CurrentFile.Text = tick.FileName;
        ProgressBar.Value = tick.Percent;
        PercentText.Text = $"{tick.Percent:F0}%";
        var etaSeconds = (tick.TotalLines - tick.LinesDone) * 0.1;
        StatsText.Text = $"{tick.LinesDone}/{tick.TotalLines} lines · ETA {etaSeconds:F0}s";

        var timeRun = new Run(tick.Timestamp);
        if (this.TryFindResource("AppLogTime", out var resource) && resource is IBrush brush)
        {
            timeRun.Foreground = brush;
        }

        if (LogText.Inlines is { } inlines)
        {
            inlines.Add(timeRun);
            inlines.Add(new Run($" {tick.Message}"));
            inlines.Add(new LineBreak());
        }

        Dispatcher.UIThread.Post(ScrollLogToEnd);
    }

    private void ScrollLogToEnd()
    {
        var scrollViewer = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer is null)
        {
            return;
        }

        var offset = scrollViewer.Offset;
        scrollViewer.Offset = new Vector(
            offset.X,
            Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));
    }

    private void OnPause(object? sender, RoutedEventArgs e)
    {
        DummySyncEngine.TogglePause();
        var paused = DummySyncEngine.IsPaused;
        SyncStatusStore.Set(paused ? SyncStatus.Paused : SyncStatus.Running);
        PauseLabel.Text = paused ? "Resume" : "Pause";
        if (this.TryFindResource(paused ? "IconPlay" : "IconPause", out var resource))
        {
            if (resource is Geometry geometry)
            {
                PauseIcon.Data = geometry;
            }
        }
    }

    private void OnFailed()
    {
        if (this.IsAttachedToVisualTree())
        {
            Navigation.ShowFailed(this);
        }
    }

    private void OnCompleted()
    {
        if (this.IsAttachedToVisualTree())
        {
            Navigation.ShowDone(this);
        }
    }
}
