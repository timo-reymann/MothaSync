using Avalonia;
using Avalonia.Controls;

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
}
