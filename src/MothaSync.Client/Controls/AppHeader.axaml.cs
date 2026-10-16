using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MothaSync.Client.Views;

namespace MothaSync.Client.Controls;

public enum HeaderTab
{
    None,
    History,
    Settings,
}

public partial class AppHeader : UserControl
{
    public static readonly StyledProperty<HeaderTab> ActiveProperty =
        AvaloniaProperty.Register<AppHeader, HeaderTab>(nameof(Active));

    static AppHeader()
    {
        ActiveProperty.Changed.AddClassHandler<AppHeader>((header, _) => header.ApplyActive());
    }

    public AppHeader()
    {
        InitializeComponent();
        HeaderBorder.AddHandler(
            InputElement.PointerPressedEvent,
            OnHeaderPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        ApplyStatus();
        ApplyActive();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncStatusStore.Changed += OnStatusChanged;
        ApplyStatus();
        MinimizeButton.IsVisible = TopLevel.GetTopLevel(this) is MainWindow;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SyncStatusStore.Changed -= OnStatusChanged;
        base.OnDetachedFromVisualTree(e);
    }

    public HeaderTab Active
    {
        get => GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    private void OnStatusChanged() => ApplyStatus();

    private void ApplyStatus()
    {
        Dot.Classes.Clear();
        Dot.Classes.Add("dot");
        Dot.Classes.Add(SyncStatusStore.Current switch
        {
            SyncStatus.Waiting => "wait",
            SyncStatus.Running => "run",
            SyncStatus.Paused => "wait",
            SyncStatus.Ok => "ok",
            SyncStatus.Error => "err",
            _ => "idle",
        });
    }

    private void ApplyActive()
    {
        (HistoryButton.Classes as IPseudoClasses).Set(":active", Active == HeaderTab.History);
        (SettingsButton.Classes as IPseudoClasses).Set(":active", Active == HeaderTab.Settings);
    }

    private void OnHistory(object? sender, RoutedEventArgs e)
    {
        if (Active == HeaderTab.History)
        {
            Navigation.ShowOverview(this);
            return;
        }

        Navigation.ShowHistory(this);
    }

    private void OnSettings(object? sender, RoutedEventArgs e)
    {
        if (Active == HeaderTab.Settings)
        {
            Navigation.ShowOverview(this);
            return;
        }

        Navigation.ShowSettings(this);
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed ||
            IsDescendantOf(e.Source as Visual, HeaderActions))
        {
            return;
        }

        if (IsDescendantOf(e.Source as Visual, HeaderIdentity))
        {
            Navigation.ShowOverview(this);
            return;
        }

        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }

    private static bool IsDescendantOf(Visual? visual, Visual ancestor)
    {
        while (visual is not null)
        {
            if (ReferenceEquals(visual, ancestor))
            {
                return true;
            }

            visual = visual.GetVisualParent();
        }

        return false;
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
        Navigation.CloseOrExit(this);
    }
}