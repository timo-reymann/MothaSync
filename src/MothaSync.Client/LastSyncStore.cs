using System;
using System.IO;
using System.Text.Json;

namespace MothaSync.Client;

public static class LastSyncStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MothaSync",
        "last-sync.json");

    public static DateTimeOffset? GetLastCompleted()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var info = JsonSerializer.Deserialize<LastSyncInfo>(File.ReadAllText(FilePath), JsonOptions);
                return info?.LastCompletedUtc;
            }
        }
        catch (JsonException)
        {
            // Treat an invalid file as "never synced".
        }
        catch (IOException)
        {
            // Same as above when the file cannot be read.
        }

        return null;
    }

    public static void MarkCompleted()
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var info = new LastSyncInfo { LastCompletedUtc = DateTimeOffset.UtcNow };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(info, JsonOptions));
    }

    private sealed class LastSyncInfo
    {
        public DateTimeOffset? LastCompletedUtc { get; set; }
    }
}
