using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using MothaSync.Client.Views;

namespace MothaSync.Client;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DriveStatusMonitor.Start();
        SyncStatusStore.Changed += () => Dispatcher.UIThread.Post(UpdateTrayIcon);
        UpdateTrayIcon();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        UserNotifications.Error += OnError;

        base.OnFrameworkInitializationCompleted();
    }

    private void OnError(string title, string message)
    {
        SystemNotifier.ShowError(title, message);
    }

    private static string TrayIconName(SyncStatus status) => status switch
    {
        SyncStatus.Waiting => "waiting",
        SyncStatus.Running => "syncing",
        SyncStatus.Paused => "paused",
        SyncStatus.Ok => "ok",
        SyncStatus.Error => "error",
        _ => "idle",
    };

    private void UpdateTrayIcon()
    {
        var tray = TrayIcon.GetIcons(this)?.FirstOrDefault();
        if (tray is null)
        {
            return;
        }

        tray.ToolTipText = $"MothaSync v{AppVersion.Current}";
        var uri = new Uri($"avares://MothaSync.Client/Assets/Trays/{TrayIconName(SyncStatusStore.Current)}.png");
        tray.Icon = new WindowIcon(AssetLoader.Open(uri));
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnOpen(object? sender, EventArgs e) => ShowMainWindow();

    private void OnExit(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void ShowMainWindow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
