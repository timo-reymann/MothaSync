namespace MothaSync.Client.Views;

public partial class HistoryDetailWindow : ChromelessWindow
{
    public HistoryDetailWindow()
    {
        InitializeComponent();
    }

    public HistoryDetailWindow(RunInfo run) : this()
    {
        Detail.Show(run);
    }
}
