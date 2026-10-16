using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace MothaSync.Client;

internal static partial class LogTextRenderer
{
    public static void Render(Control owner, TextBlock target, string? text)
    {
        target.Inlines?.Clear();
        if (string.IsNullOrWhiteSpace(text))
        {
            target.Text = "No rclone output is available.";
            return;
        }

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Append(owner, target.Inlines!, line.TrimEnd('\r'));
            target.Inlines!.Add(new LineBreak());
        }
    }

    public static void Append(Control owner, InlineCollection inlines, string line)
    {
        var match = LogLinePattern().Match(line);
        if (!match.Success)
        {
            inlines.Add(new Run(line));
            return;
        }

        inlines.Add(new Run(match.Groups[1].Value) { Foreground = Brush(owner, "AppLogTime") });
        inlines.Add(new Run($" {match.Groups[2].Value}")
        {
            Foreground = Brush(owner, LevelBrush(match.Groups[2].Value))
        });
        inlines.Add(new Run($"  {match.Groups[3].Value}")
        {
            Foreground = Brush(owner, "AppText")
        });
    }

    private static string LevelBrush(string level) => level.ToUpperInvariant() switch
    {
        "ERROR" => "AppErr",
        "WARN" or "WARNING" or "NOTICE" => "AppWarn",
        "INFO" => "AppSync",
        _ => "AppMuted"
    };

    private static IBrush? Brush(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var resource) ? resource as IBrush : null;

    [GeneratedRegex(@"^\s*(\d{2}:\d{2}:\d{2})\s+(INFO|NOTICE|WARN|WARNING|ERROR|DEBUG)\s{2,}(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex LogLinePattern();
}
