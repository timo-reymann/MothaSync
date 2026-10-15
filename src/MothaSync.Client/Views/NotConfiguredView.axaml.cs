using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class NotConfiguredView : UserControl
{
    public NotConfiguredView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncStatusStore.Set(SyncStatus.Idle);
    }

    private void OnConfigure(object? sender, RoutedEventArgs e)
    {
        Navigation.ShowSettings(this);
    }
}