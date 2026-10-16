using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class FailedView : UserControl
{
    public FailedView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncStatusStore.Set(SyncStatus.Error);
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DummySyncEngine.IsRunning)
        {
            return;
        }

        Navigation.ShowSyncing(this);
        DummySyncEngine.Start();
    }
}
