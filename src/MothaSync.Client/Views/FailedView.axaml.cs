using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;

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
        var run = new RunLogStore().GetRecentRuns(1).FirstOrDefault();
        if (run is not null && !string.IsNullOrWhiteSpace(run.LogText))
        {
            LogTextRenderer.Render(this, LogText, run.LogText);
        }
    }

    private void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (RcloneSyncEngine.IsRunning)
        {
            return;
        }

        Navigation.ShowSyncing(this);
        RcloneSyncEngine.Start();
    }
}
