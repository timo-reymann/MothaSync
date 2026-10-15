using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace MothaSync.Client.Views;

public partial class HistoryDetailView : UserControl
{
    public HistoryDetailView()
    {
        InitializeComponent();
    }

    public void Show(RunInfo run)
    {
        DateText.Text = run.Date;
        FilesText.Text = run.Files;
        SizeText.Text = run.Size;
        DurationText.Text = run.Duration;
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

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        Navigation.GoBack(this);
    }
}