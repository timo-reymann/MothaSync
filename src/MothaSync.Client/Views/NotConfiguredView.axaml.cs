using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class NotConfiguredView : UserControl
{
    public NotConfiguredView()
    {
        InitializeComponent();
    }

    private async void OnConfigure(object? sender, RoutedEventArgs e)
    {
        await Dialogs.ShowSettings(this);
    }
}
