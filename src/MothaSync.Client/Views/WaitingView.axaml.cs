using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace MothaSync.Client.Views;

public partial class WaitingView : UserControl
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private readonly DispatcherTimer _timer = new() { Interval = TickInterval };

    public WaitingView()
    {
        _timer.Tick += (_, _) => RefreshSyncState();
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SettingsStore.Changed += OnSettingsChanged;
        DriveStatusMonitor.Changed += OnDriveStatusChanged;
        ApplySettings();
        ApplyDriveStatus();
        PublishStatus();
        RefreshSyncState();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        SettingsStore.Changed -= OnSettingsChanged;
        DriveStatusMonitor.Changed -= OnDriveStatusChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSettingsChanged() => ApplySettings();

    private void OnDriveStatusChanged()
    {
        ApplyDriveStatus();
        PublishStatus();
        RefreshSyncState();
    }

    private static void PublishStatus()
    {
        var ready = DriveStatusMonitor.SourceConnected && DriveStatusMonitor.DestinationConnected;
        SyncStatusStore.Set(ready ? SyncStatus.Ok : SyncStatus.Waiting);
    }

    private void RefreshSyncState()
    {
        var bothConnected = DriveStatusMonitor.SourceConnected && DriveStatusMonitor.DestinationConnected;
        var lastSync = LastSyncStore.GetLastCompleted();
        var now = DateTimeOffset.UtcNow;
        var due = SyncSchedule.IsDue(lastSync, now);

        LastSyncText.Text = SyncSchedule.FormatLastSync(lastSync, now);
        SyncNowButton.IsVisible = bothConnected && !DummySyncEngine.IsRunning && !due;
        SyncNowButton.Content = due ? "Sync" : $"Sync ({SyncSchedule.FormatCountdown(SyncSchedule.Remaining(lastSync, now))})";
        if (!bothConnected || DummySyncEngine.IsRunning || !due)
        {
            return;
        }

        StartSync();
    }

    private void OnSyncNow(object? sender, RoutedEventArgs e)
    {
        if (DriveStatusMonitor.SourceConnected &&
            DriveStatusMonitor.DestinationConnected &&
            !DummySyncEngine.IsRunning)
        {
            StartSync();
        }
    }

    private void StartSync()
    {
        SyncNowButton.IsVisible = false;
        Navigation.ShowSyncing(this);
        DummySyncEngine.Start();
    }

    private void ApplySettings()
    {
        var settings = SettingsStore.Load();
        SourceTitle.Text = FormatTitle("Drive A", settings.SourceLabel);
        DestinationTitle.Text = FormatTitle("Drive B", settings.DestinationLabel);
        SourcePathText.Text = string.IsNullOrWhiteSpace(settings.SourcePath) ? "not set" : settings.SourcePath;
        DestinationPathText.Text = string.IsNullOrWhiteSpace(settings.DestinationPath) ? "not set" : settings.DestinationPath;
    }

    private void ApplyDriveStatus()
    {
        ApplyDriveStatus(DriveStatusMonitor.SourceConnected, SourceDot, SourceStatusText);
        ApplyDriveStatus(DriveStatusMonitor.DestinationConnected, DestinationDot, DestinationStatusText);
    }

    private static void ApplyDriveStatus(bool connected, Ellipse dot, TextBlock label)
    {
        if (connected)
        {
            dot.Classes.Add("ok");
            label.Text = "connected";
        }
        else
        {
            dot.Classes.Remove("ok");
            label.Text = "waiting for drive";
        }
    }

    private static string FormatTitle(string role, string label)
    {
        return string.IsNullOrWhiteSpace(label) ? role : $"{role} · {label.Trim()}";
    }
}
