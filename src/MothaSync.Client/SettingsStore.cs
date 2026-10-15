using System;
using System.IO;
using System.Text.Json;

namespace MothaSync.Client;

public sealed class AppSettings
{
    public string SourcePath { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string DestinationLabel { get; set; } = string.Empty;
    public string ServerUrl { get; set; } = string.Empty;
    public string EnrollmentToken { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static event Action? Changed;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MothaSync",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions)
                       ?? new AppSettings();
            }
        }
        catch (JsonException)
        {
            // Start with empty settings when the file is invalid.
        }
        catch (IOException)
        {
            // Start with empty settings when the file cannot be read.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings, bool notify = true)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        if (notify)
        {
            Changed?.Invoke();
        }
    }
}