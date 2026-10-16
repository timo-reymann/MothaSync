using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace MothaSync.Client.Views;

public partial class SettingsView : UserControl
{
    private string _sourceAutoLabel = string.Empty;
    private string _destinationAutoLabel = string.Empty;
    private bool _loading;
    private bool _urlValid = true;

    public SettingsView()
    {
        InitializeComponent();
        LoadSettings();
        ServerUrl.TextChanged += (_, _) => ValidateServerUrl();
        SourcePath.TextChanged += (_, _) =>
        {
            UpdateAutoLabel(SourcePath, SourceLabel, ref _sourceAutoLabel);
            ApplySourceStatus();
            ValidatePaths();
        };
        DestinationPath.TextChanged += (_, _) =>
        {
            UpdateAutoLabel(DestinationPath, DestinationLabel, ref _destinationAutoLabel);
            ApplyDestinationStatus();
            ValidatePaths();
        };
        ValidateServerUrl();
        ValidatePaths();
        ApplySourceStatus();
        ApplyDestinationStatus();
    }

    private void ApplySourceStatus() =>
        ApplyDriveStatus(PathExists(SourcePath.Text), SourceDot, SourceStatusText);

    private void ApplyDestinationStatus() =>
        ApplyDriveStatus(PathExists(DestinationPath.Text), DestinationDot, DestinationStatusText);

    private static bool PathExists(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path.Trim());

    private static void ApplyDriveStatus(bool connected, Ellipse dot, TextBlock label)
    {
        if (connected)
        {
            dot.Classes.Add("ok");
            label.Text = "connected";
        }
        else
        {
            dot.Classes.Remove("ok");
            label.Text = "waiting for drive";
        }
    }

    private void LoadSettings()
    {
        _loading = true;
        var settings = SettingsStore.Load();
        SourcePath.Text = settings.SourcePath;
        DestinationPath.Text = settings.DestinationPath;
        ServerUrl.Text = settings.ServerUrl;
        EnrollmentToken.Text = settings.EnrollmentToken;
        _sourceAutoLabel = GetVolumeLabel(SourcePath.Text) ?? string.Empty;
        _destinationAutoLabel = GetVolumeLabel(DestinationPath.Text) ?? string.Empty;
        SourceLabel.Text = string.IsNullOrWhiteSpace(settings.SourceLabel)
            ? _sourceAutoLabel
            : settings.SourceLabel;
        DestinationLabel.Text = string.IsNullOrWhiteSpace(settings.DestinationLabel)
            ? _destinationAutoLabel
            : settings.DestinationLabel;
        _loading = false;
    }

    private void ValidateServerUrl()
    {
        var text = ServerUrl.Text?.Trim();
        var isValid = string.IsNullOrEmpty(text) || IsValidUrl(text);

        if (isValid)
        {
            ServerUrl.Classes.Remove("err");
            UrlValidationMessage.IsVisible = false;
            _urlValid = true;
        }
        else
        {
            ServerUrl.Classes.Add("err");
            UrlValidationMessage.IsVisible = true;
            _urlValid = false;
        }

        UpdateSaveEnabled();
    }

    private void ValidatePaths()
    {
        var same = SyncPaths.AreSame(SourcePath.Text, DestinationPath.Text);
        SamePathMessage.IsVisible = same;
        SourcePath.Classes.Set("err", same);
        DestinationPath.Classes.Set("err", same);
        UpdateSaveEnabled();
    }

    private void UpdateSaveEnabled() =>
        SaveButton.IsEnabled = _urlValid && !SamePathMessage.IsVisible;

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
               && !string.IsNullOrWhiteSpace(uri.Host);
    }

    private async void OnTestEnrollment(object? sender, RoutedEventArgs e)
    {
        var url = ServerUrl.Text?.Trim() ?? string.Empty;
        var token = EnrollmentToken.Text?.Trim() ?? string.Empty;
        if (!IsValidUrl(url) || string.IsNullOrEmpty(token))
        {
            ShowTestResult("Enter a server URL and enrollment token first.", ok: false);
            return;
        }

        TestEnrollmentButton.IsEnabled = false;
        ShowTestResult("Testing…", ok: null);
        var result = await ServerReporter.TestEnrollmentAsync(url, token);
        TestEnrollmentButton.IsEnabled = true;
        switch (result)
        {
            case ServerReporter.EnrollmentCheck.Valid:
                ShowTestResult("Enrollment token accepted.", ok: true);
                break;
            case ServerReporter.EnrollmentCheck.InvalidToken:
                ShowTestResult("Server reachable, but the token was rejected.", ok: false);
                break;
            default:
                ShowTestResult("Could not reach the server.", ok: false);
                break;
        }
    }

    private void ShowTestResult(string text, bool? ok)
    {
        TestEnrollmentResult.Text = text;
        var key = ok switch { true => "AppOk", false => "AppErrText", _ => "AppMuted" };
        if (this.TryFindResource(key, ActualThemeVariant, out var brush) && brush is IBrush b)
        {
            TestEnrollmentResult.Foreground = b;
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Navigation.GoBack(this);
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        // Start from the stored settings so the device registration is preserved.
        var settings = SettingsStore.Load();
        var serverUrl = ServerUrl.Text?.Trim() ?? string.Empty;
        var enrollmentToken = EnrollmentToken.Text?.Trim() ?? string.Empty;
        if (serverUrl != settings.ServerUrl || enrollmentToken != settings.EnrollmentToken)
        {
            // A different server or token means this device must register again.
            settings.DeviceId = string.Empty;
            settings.DeviceToken = string.Empty;
        }

        settings.SourcePath = SourcePath.Text?.Trim() ?? string.Empty;
        settings.SourceLabel = SourceLabel.Text?.Trim() ?? string.Empty;
        settings.DestinationPath = DestinationPath.Text?.Trim() ?? string.Empty;
        settings.DestinationLabel = DestinationLabel.Text?.Trim() ?? string.Empty;
        settings.ServerUrl = serverUrl;
        settings.EnrollmentToken = enrollmentToken;
        SettingsStore.Save(settings);
        Navigation.GoBack(this);
    }

    private void UpdateAutoLabel(TextBox pathBox, TextBox labelBox, ref string autoLabel)
    {
        if (_loading)
        {
            return;
        }

        var volumeLabel = GetVolumeLabel(pathBox.Text);
        if (string.Equals(labelBox.Text?.Trim(), autoLabel, StringComparison.OrdinalIgnoreCase))
        {
            labelBox.Text = volumeLabel ?? string.Empty;
        }

        autoLabel = volumeLabel ?? string.Empty;
    }

    private static string? GetVolumeLabel(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var drive = new DriveInfo(fullPath);
            return string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async void OnPickSource(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFolder("Choose source folder (Drive A)");
            if (!string.IsNullOrEmpty(path))
            {
                SourcePath.Text = path;
            }
        }
        catch
        {
            // Ignore folder picker cancellation/error
        }
    }

    private async void OnPickDestination(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFolder("Choose destination drive (Drive B)");
            if (!string.IsNullOrEmpty(path))
            {
                DestinationPath.Text = path;
            }
        }
        catch
        {
            // Ignore folder picker cancellation/error
        }
    }

    private async Task<string?> PickFolder(string title)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return null;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            return folders[0].TryGetLocalPath() ?? folders[0].Path.LocalPath;
        }

        return null;
    }
}