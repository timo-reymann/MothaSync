using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MothaSync.Client.Views;

public partial class LicenseView : UserControl
{
    public LicenseView()
    {
        InitializeComponent();
    }

    public void Show()
    {
        LicenseText.Text = LoadLicenseText();
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }
    }

    private void OnTitlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed &&
            TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(LicenseText.SelectedText))
        {
            LicenseText.SelectAll();
        }

        LicenseText.Copy();
    }

    // TODO: read the NOTICE file that the build copies next to the executable.
    // Show placeholder text for now.
    private static string LoadLicenseText() => """
        MothaSync
        Copyright (c) 2026 Timo Reymann

        This window shows the license information for MothaSync and the software
        that MothaSync ships. MothaSync will load this text from the NOTICE file
        when the file ships with the build.

        MothaSync is licensed under the MIT License.

        rclone provides the sync engine. rclone is licensed under the MIT License.

        Open Sans and JetBrains Mono are licensed under the SIL Open Font License 1.1.

        This is placeholder text. The NOTICE file will replace it.
        """;
}
