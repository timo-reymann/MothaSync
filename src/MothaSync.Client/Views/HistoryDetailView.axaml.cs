using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Linq;

namespace MothaSync.Client.Views;

public partial class HistoryDetailView : UserControl
{
    private RunInfo? _run;

    public HistoryDetailView()
    {
        InitializeComponent();
    }

    public void Show(RunInfo run)
    {
        _run = run;
        DateText.Text = run.Date;
        FilesText.Text = run.Files;
        SizeText.Text = run.Size;
        DurationText.Text = run.Duration;
        RenderLog();
        StatusText.Text = run.Failed ? "failed" : "completed";
        if (run.Failed && this.TryFindResource("AppErrText", ActualThemeVariant, out var errText))
        {
            StatusText.Foreground = errText as IBrush;
        }
        else
        {
            StatusText.ClearValue(TextBlock.ForegroundProperty);
        }

        ErrorPill.IsVisible = run.Failed;
        FailedLog.IsVisible = run.Failed;
        OkLog.IsVisible = !run.Failed;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RenderLog();
    }

    private void RenderLog()
    {
        if (_run is null)
        {
            return;
        }

        var logText = _run.RunId is { } runId
            ? new RunLogStore().GetRecentRuns(100).FirstOrDefault(item => item.Id == runId)?.LogText ?? _run.LogText
            : _run.LogText;
        LogTextRenderer.Render(this, FailedLogText, logText);
        LogTextRenderer.Render(this, OkLogText, logText);
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        Navigation.GoBack(this);
    }
}
