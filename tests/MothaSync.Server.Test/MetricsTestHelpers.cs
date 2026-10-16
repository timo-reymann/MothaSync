using System.Globalization;
using System.Text;
using Prometheus;

namespace MothaSync.Server.Test;

/// <summary>Shared helpers for asserting on exported prometheus-net metrics.</summary>
public static class MetricsTestHelpers
{
    public static async Task<string> ExportToTextAsync(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Returns the sample line for <paramref name="name"/> whose labels contain all
    /// the given label=value pairs, or null. Label order is irrelevant.</summary>
    public static string? FindLine(string text, string name, params (string Key, string Value)[] labels)
    {
        return text
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => LineName(line) == name)
            .FirstOrDefault(line => labels.All(label =>
                line.Contains($"{label.Key}=\"{Escape(label.Value)}\"", StringComparison.Ordinal)));
    }

    /// <summary>Parses the numeric sample value of a text-format exposition line.</summary>
    public static double ParseValue(string line)
    {
        var close = line.LastIndexOf('}');
        if (close < 0)
        {
            var space = line.IndexOf(' ');
            return double.Parse(line[(space + 1)..].Trim(), CultureInfo.InvariantCulture);
        }

        return double.Parse(line[(close + 2)..].Trim(), CultureInfo.InvariantCulture);
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string LineName(string line)
    {
        var brace = line.IndexOf('{');
        var space = line.IndexOf(' ');
        if (space < 0)
        {
            return brace < 0 ? line : line[..brace];
        }

        return line[..Math.Min(brace < 0 ? space : space < brace ? space : brace, line.Length)].Trim();
    }
}
