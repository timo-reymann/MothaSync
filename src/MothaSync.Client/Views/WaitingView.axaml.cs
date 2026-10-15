using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace MothaSync.Client.Views;

public partial class WaitingView : UserControl
{
    public WaitingView()
    {
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
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
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
        var shouldAutoSync = ShouldAutoSync();

        SyncNowButton.IsVisible = bothConnected && !DummySyncEngine.IsRunning && !shouldAutoSync;
        if (!bothConnected || DummySyncEngine.IsRunning || !shouldAutoSync)
        {
            return;
        }

        StartSync();
    }

    private static bool ShouldAutoSync()
    {
        var lastSync = LastSyncStore.GetLastCompleted();
        return lastSync is null || DateTimeOffset.UtcNow - lastSync.Value > TimeSpan.FromHours(1);
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
