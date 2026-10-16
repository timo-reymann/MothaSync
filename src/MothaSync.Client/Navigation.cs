using Avalonia;
using Avalonia.Controls;
using MothaSync.Client.Views;

namespace MothaSync.Client;

public static class Navigation
{
    public static void ShowOverview(Visual from)
    {
        GetMainWindow(from)?.NavigateHome();
    }

    public static void ShowSettings(Visual from)
    {
        GetMainWindow(from)?.NavigateTo(new SettingsView());
    }

    public static void ShowSyncing(Visual from)
    {
        GetMainWindow(from)?.NavigateTo(new SyncingView());
    }

    public static void ShowDone(Visual from)
    {
        GetMainWindow(from)?.NavigateTo(new DoneView());
    }

    public static void ShowFailed(Visual from)
    {
        GetMainWindow(from)?.NavigateTo(new FailedView());
    }

    public static void ShowHistory(Visual from)
    {
        GetMainWindow(from)?.NavigateTo(new HistoryView());
    }

    public static void ShowHistoryDetail(Visual from, RunInfo run)
    {
        var detailView = new HistoryDetailView();
        detailView.Show(run);
        GetMainWindow(from)?.NavigateTo(detailView);
    }

    public static void GoBack(Visual from)
    {
        GetMainWindow(from)?.NavigateBack();
    }

    public static void CloseOrExit(Visual from)
    {
        if (TopLevel.GetTopLevel(from) is Window window)
        {
            window.Hide();
        }
    }

    private static MainWindow? GetMainWindow(Visual from)
    {
        if (TopLevel.GetTopLevel(from) is MainWindow main)
        {
            return main;
        }

        return Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: MainWindow mw
            }
            ? mw
            : null;
    }
}