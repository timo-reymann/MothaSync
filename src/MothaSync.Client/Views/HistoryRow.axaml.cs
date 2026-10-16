using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class HistoryRow : UserControl
{
    public static readonly StyledProperty<string> DateProperty =
        AvaloniaProperty.Register<HistoryRow, string>(nameof(Date), string.Empty);

    public static readonly StyledProperty<string> FilesProperty =
        AvaloniaProperty.Register<HistoryRow, string>(nameof(Files), string.Empty);

    public static readonly StyledProperty<string> SizeProperty =
        AvaloniaProperty.Register<HistoryRow, string>(nameof(Size), string.Empty);

    public static readonly StyledProperty<string> DurationProperty =
        AvaloniaProperty.Register<HistoryRow, string>(nameof(Duration), string.Empty);

    public static readonly StyledProperty<bool> FailedProperty =
        AvaloniaProperty.Register<HistoryRow, bool>(nameof(Failed));

    public static readonly StyledProperty<string> LogTextProperty =
        AvaloniaProperty.Register<HistoryRow, string>(nameof(LogText), string.Empty);

    public static readonly StyledProperty<long> RunIdProperty =
        AvaloniaProperty.Register<HistoryRow, long>(nameof(RunId));

    static HistoryRow()
    {
        FailedProperty.Changed.AddClassHandler<HistoryRow>((row, _) => row.ApplyFailed());
    }

    public HistoryRow()
    {
        InitializeComponent();
    }

    public string Date
    {
        get => GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public string Files
    {
        get => GetValue(FilesProperty);
        set => SetValue(FilesProperty, value);
    }

    public string Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public string Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public bool Failed
    {
        get => GetValue(FailedProperty);
        set => SetValue(FailedProperty, value);
    }

    public string LogText
    {
        get => GetValue(LogTextProperty);
        set => SetValue(LogTextProperty, value);
    }

    public long RunId
    {
        get => GetValue(RunIdProperty);
        set => SetValue(RunIdProperty, value);
    }

    private void ApplyFailed()
    {
        OkDot.IsVisible = !Failed;
        OkIcon.IsVisible = !Failed;
        ErrDot.IsVisible = Failed;
        WarnIcon.IsVisible = Failed;
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        Navigation.ShowHistoryDetail(this, new RunInfo(Date, Files, Size, Duration, Failed, LogText, RunId));
    }
}
