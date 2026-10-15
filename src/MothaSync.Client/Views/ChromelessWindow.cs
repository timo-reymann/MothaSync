using Avalonia.Controls;
using Avalonia.Input;

namespace MothaSync.Client.Views;

public class ChromelessWindow : Window
{
    public ChromelessWindow()
    {
        Width = 700;
        Height = 700;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    public bool CloseOnEscape { get; set; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (CloseOnEscape && e.Key == Key.Escape)
        {
            Close();
        }
    }
}