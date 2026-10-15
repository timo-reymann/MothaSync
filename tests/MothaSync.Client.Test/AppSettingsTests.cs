using System.Text.Json;

namespace MothaSync.Client.Test;

public sealed class AppSettingsTests
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [Fact]
    public void Defaults_AreEmptyStrings()
    {
        var settings = new AppSettings();

        Assert.Equal(string.Empty, settings.SourcePath);
        Assert.Equal(string.Empty, settings.SourceLabel);
        Assert.Equal(string.Empty, settings.DestinationPath);
        Assert.Equal(string.Empty, settings.DestinationLabel);
        Assert.Equal(string.Empty, settings.ServerUrl);
        Assert.Equal(string.Empty, settings.EnrollmentToken);
        Assert.Equal(string.Empty, settings.DeviceId);
        Assert.Equal(string.Empty, settings.DeviceToken);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var settings = new AppSettings
        {
            SourcePath = "/mnt/source",
            SourceLabel = "Source",
            DestinationPath = "/mnt/backup",
            DestinationLabel = "Backup",
            ServerUrl = "https://sync.example.com",
            EnrollmentToken = "enroll",
            DeviceId = "device-1",
            DeviceToken = "token-1",
        };

        var json = JsonSerializer.Serialize(settings, Options);
        var restored = JsonSerializer.Deserialize<AppSettings>(json, Options);

        Assert.NotNull(restored);
        Assert.Equal(settings.SourcePath, restored.SourcePath);
        Assert.Equal(settings.SourceLabel, restored.SourceLabel);
        Assert.Equal(settings.DestinationPath, restored.DestinationPath);
        Assert.Equal(settings.DestinationLabel, restored.DestinationLabel);
        Assert.Equal(settings.ServerUrl, restored.ServerUrl);
        Assert.Equal(settings.EnrollmentToken, restored.EnrollmentToken);
        Assert.Equal(settings.DeviceId, restored.DeviceId);
        Assert.Equal(settings.DeviceToken, restored.DeviceToken);
    }
}
