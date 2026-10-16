using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.Win32;

namespace MothaSync.Client;

internal static class AutoStartRegistration
{
    private const string Argument = "--mothasync-autostart";
    private const string ApplicationId = "de.timo_reymann.mothasync";

    public static void EnsureRegistered()
    {
        try
        {
            var executable = Environment.ProcessPath
                             ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executable))
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                RegisterWindows(executable);
            }
            else if (OperatingSystem.IsMacOS())
            {
                RegisterMacOs(executable);
            }
            else if (OperatingSystem.IsLinux())
            {
                RegisterLinux(executable);
            }
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"Could not register MothaSync for autostart: {exception}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(string executable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");
        key?.SetValue(ApplicationId, $"{QuoteWindows(executable)} {Argument}");
    }

    private static void RegisterMacOs(string executable)
    {
        var launchAgents = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "LaunchAgents");
        Directory.CreateDirectory(launchAgents);

        var logs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "Logs",
            "MothaSync");
        Directory.CreateDirectory(logs);

        var plist = new XElement("plist",
            new XAttribute("version", "1.0"),
            new XElement("dict",
                new XElement("key", "Label"),
                new XElement("string", ApplicationId),
                new XElement("key", "ProgramArguments"),
                new XElement("array", new XElement("string", executable), new XElement("string", Argument)),
                new XElement("key", "RunAtLoad"),
                new XElement("true"),
                new XElement("key", "AbandonProcessGroup"),
                new XElement("true"),
                new XElement("key", "StandardOutPath"),
                new XElement("string", Path.Combine(logs, "autostart-stdout.log")),
                new XElement("key", "StandardErrorPath"),
                new XElement("string", Path.Combine(logs, "autostart-stderr.log"))));

        var path = Path.Combine(launchAgents, $"{ApplicationId}.plist");
        new XDocument(new XDeclaration("1.0", "UTF-8", null), plist).Save(path);
    }

    private static void RegisterLinux(string executable)
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configDirectory = string.IsNullOrWhiteSpace(configHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : configHome;
        var autostartDirectory = Path.Combine(configDirectory, "autostart");
        Directory.CreateDirectory(autostartDirectory);

        var lines = new List<string>
        {
            "[Desktop Entry]",
            "Type=Application",
            "Name=MothaSync",
            $"Exec={QuoteDesktop(executable)} {Argument}",
            "X-GNOME-Autostart-enabled=true",
            "Hidden=false"
        };
        var icon = Path.Combine(AppContext.BaseDirectory, "logo.png");
        if (File.Exists(icon))
        {
            lines.Add($"Icon={icon}");
        }

        File.WriteAllLines(Path.Combine(autostartDirectory, "mothasync.desktop"), lines);
    }

    private static string QuoteWindows(string value) => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static string QuoteDesktop(string value) =>
        $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

}
