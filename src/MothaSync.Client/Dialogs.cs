using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using MothaSync.Client.Views;

namespace MothaSync.Client;

internal static class Dialogs
{
    public static Task ShowSettings(Visual from)
    {
        Navigation.ShowSettings(from);
        return Task.CompletedTask;
    }

    public static Task ShowHistory(Visual from)
    {
        Navigation.ShowHistory(from);
        return Task.CompletedTask;
    }

    public static Task ShowHistoryDetail(Visual from, RunInfo run)
    {
        Navigation.ShowHistoryDetail(from, run);
        return Task.CompletedTask;
    }

    public static Task ShowLicense(Visual? from)
    {
        if (GetOwnerWindow(from) is not { } owner)
        {
            return Task.CompletedTask;
        }

        return new LicenseWindow().ShowDialog(owner);
    }

    private static Window? GetOwnerWindow(Visual? from)
    {
        if (from is not null && TopLevel.GetTopLevel(from) is Window window)
        {
            return window;
        }

        return Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: Window main
            }
            ? main
            : null;
    }

    public static void CloseWindowOf(Visual from)
    {
        Navigation.GoBack(from);
    }
}