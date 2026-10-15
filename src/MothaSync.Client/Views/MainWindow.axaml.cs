using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace MothaSync.Client.Views;

public partial class MainWindow : ChromelessWindow
{
    private readonly Stack<Control> _backStack = new();
    private readonly Control _rootView;

    public MainWindow()
    {
        InitializeComponent();
        _rootView = new WaitingView();
        ViewHost.Content = _rootView;
    }

    public MainWindow(Control initialView)
    {
        InitializeComponent();
        _rootView = initialView;
        ViewHost.Content = _rootView;
    }

    public void NavigateTo(Control view)
    {
        if (ViewHost.Content is Control currentView)
        {
            if (currentView.GetType() == view.GetType())
            {
                return;
            }

            _backStack.Push(currentView);
        }

        ViewHost.Content = view;
    }

    public void NavigateBack()
    {
        if (_backStack.TryPop(out var previousView))
        {
            ViewHost.Content = previousView;
        }
        else if (!ReferenceEquals(ViewHost.Content, _rootView))
        {
            ViewHost.Content = _rootView;
        }
    }

    public void NavigateHome()
    {
        _backStack.Clear();
        ViewHost.Content = _rootView;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_backStack.Count > 0 || !ReferenceEquals(ViewHost.Content, _rootView))
        {
            NavigateBack();
            e.Handled = true;
        }
    }
}