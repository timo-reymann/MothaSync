using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using MothaSync.Client.Views;

namespace MothaSync.Client;

internal static class Dialogs
{
    public static Task ShowSettings(Visual from) => Show(from, new SettingsWindow());

    public static Task ShowHistory(Visual from) => Show(from, new HistoryWindow());

    public static Task ShowHistoryDetail(Visual from, RunInfo run) => Show(from, new HistoryDetailWindow(run));

    public static void CloseWindowOf(Visual from)
    {
        if (TopLevel.GetTopLevel(from) is Window window)
        {
            window.Close();
        }
    }

    private static Task Show(Visual from, Window dialog)
    {
        if (TopLevel.GetTopLevel(from) is Window owner)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            return dialog.ShowDialog(owner);
        }

        dialog.Show();
        return Task.CompletedTask;
    }
}
