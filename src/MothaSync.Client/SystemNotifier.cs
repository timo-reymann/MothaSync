using System;
using System.Diagnostics;
using System.IO;

namespace MothaSync.Client;

internal static class SystemNotifier
{
    public static void ShowError(string title, string message)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                if (!ShowMacOsWithIcon(title, message))
                {
                    ShowMacOs(title, message);
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var icon = NotificationIconPath();
                if (icon is null)
                {
                    Start("notify-send", title, message);
                }
                else
                {
                    Start("notify-send", "-i", icon, title, message);
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                Start("msg.exe", "*", $"{title}: {message}");
            }
        }
        catch (Exception)
        {
            // Native notifications are best effort; the in-app notification remains available.
        }
    }

    private static void ShowMacOs(string title, string message)
    {
        var script = $"display notification {AppleScriptString(message)} with title {AppleScriptString(title)}";
        Start("/usr/bin/osascript", "-e", script);
    }

    private static bool ShowMacOsWithIcon(string title, string message)
    {
        var executable = FindExecutable("terminal-notifier");
        var icon = NotificationIconPath();
        if (executable is null || icon is null)
        {
            return false;
        }

        Start(executable, "-title", title, "-message", message, "-appIcon", icon, "-group", "mothasync");
        return true;
    }

    private static string? NotificationIconPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "logo.png"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "MothaSync.icns"))
        };
        return Array.Find(candidates, File.Exists);
    }

    private static string? FindExecutable(string name)
    {
        var candidates = OperatingSystem.IsMacOS()
            ? new[] { "/opt/homebrew/bin/terminal-notifier", "/usr/local/bin/terminal-notifier" }
            : Array.Empty<string>();
        return Array.Find(candidates, File.Exists);
    }

    private static void Start(string executable, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
    }

    private static string AppleScriptString(string value) =>
        $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
