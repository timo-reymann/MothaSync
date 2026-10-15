using System.Threading.Tasks;
using Avalonia;
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

    public static void CloseWindowOf(Visual from)
    {
        Navigation.GoBack(from);
    }
}