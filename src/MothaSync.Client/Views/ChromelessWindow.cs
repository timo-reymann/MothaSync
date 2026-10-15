using Avalonia.Controls;
using Avalonia.Input;

namespace MothaSync.Client.Views;

public class ChromelessWindow : Window
{
    public ChromelessWindow()
    {
        Width = 640;
        Height = 600;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    public bool CloseOnEscape { get; set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (CloseOnEscape && e.Key == Key.Escape)
        {
            Close();
        }
    }
}
