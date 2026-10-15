using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MothaSync.Client.Views;

namespace MothaSync.Client.Controls;

public enum SyncStatus
{
    Idle,
    Running,
    Ok,
    Error,
}

public enum HeaderTab
{
    None,
    History,
    Settings,
}

public partial class AppHeader : UserControl
{
    public static readonly StyledProperty<SyncStatus> StatusProperty =
        AvaloniaProperty.Register<AppHeader, SyncStatus>(nameof(Status));

    public static readonly StyledProperty<HeaderTab> ActiveProperty =
        AvaloniaProperty.Register<AppHeader, HeaderTab>(nameof(Active));

    static AppHeader()
    {
        StatusProperty.Changed.AddClassHandler<AppHeader>((header, _) => header.ApplyStatus());
        ActiveProperty.Changed.AddClassHandler<AppHeader>((header, _) => header.ApplyActive());
    }

    public AppHeader()
    {
        InitializeComponent();
        ApplyStatus();
        ApplyActive();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        MinimizeButton.IsVisible = TopLevel.GetTopLevel(this) is MainWindow;
    }

    public SyncStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public HeaderTab Active
    {
        get => GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    private void ApplyStatus()
    {
        Dot.Classes.Clear();
        Dot.Classes.Add("dot");
        Dot.Classes.Add(Status switch
        {
            SyncStatus.Running => "run",
            SyncStatus.Ok => "ok",
            SyncStatus.Error => "err",
            _ => "idle",
        });
    }

    private void ApplyActive()
    {
        ((IPseudoClasses)HistoryButton.Classes).Set(":active", Active == HeaderTab.History);
        ((IPseudoClasses)SettingsButton.Classes).Set(":active", Active == HeaderTab.Settings);
    }

    private async void OnHistory(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow)
        {
            await Dialogs.ShowHistory(this);
        }
    }

    private async void OnSettings(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow)
        {
            await Dialogs.ShowSettings(this);
        }
    }

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void OnExit(object? sender, RoutedEventArgs e)
    {
        Dialogs.CloseWindowOf(this);
    }
}
