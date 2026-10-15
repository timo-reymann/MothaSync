using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        Dialogs.CloseWindowOf(this);
    }
}
