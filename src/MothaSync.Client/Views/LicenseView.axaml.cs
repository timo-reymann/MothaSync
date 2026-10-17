using System;
using System.IO;
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

    // The build bundles NOTICE next to the executable. A missing file is a
    // packaging error. Loading then fails hard instead of showing a fallback.
    private static string LoadLicenseText() => LoadLicenseText(
        Path.Combine(AppContext.BaseDirectory, "NOTICE"));

    internal static string LoadLicenseText(string noticePath) => File.ReadAllText(noticePath);
}
