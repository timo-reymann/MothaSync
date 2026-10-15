using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class DoneView : UserControl
{
    public DoneView()
    {
        InitializeComponent();
    }

    private async void OnViewHistory(object? sender, RoutedEventArgs e)
    {
        await Dialogs.ShowHistory(this);
    }
}
