using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace MothaSync.Client.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Dialogs.CloseWindowOf(this);
    }

    private async void OnPickSource(object? sender, RoutedEventArgs e)
    {
        await PickFolder("Choose the folder to back up");
    }

    private async void OnPickDestination(object? sender, RoutedEventArgs e)
    {
        await PickFolder("Choose the destination drive");
    }

    private async Task PickFolder(string title)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
    }
}
