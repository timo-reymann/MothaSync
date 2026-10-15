using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class DoneView : UserControl
{
    public DoneView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncStatusStore.Set(SyncStatus.Ok);
    }

    private void OnViewHistory(object? sender, RoutedEventArgs e)
    {
        Navigation.ShowHistory(this);
    }

    private void OnBackToOverview(object? sender, RoutedEventArgs e)
    {
        Navigation.ShowOverview(this);
    }
}