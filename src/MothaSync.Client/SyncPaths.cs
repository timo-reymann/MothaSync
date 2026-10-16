using System;
using System.IO;

namespace MothaSync.Client;

public static class SyncPaths
{
    public static bool AreSame(string? source, string? destination)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            return false;
        }

        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(Normalize(source), Normalize(destination), comparison);
    }

    private static string Normalize(string path)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path.Trim());
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            full = path.Trim();
        }

        var root = Path.GetPathRoot(full);
        return full.Length > (root?.Length ?? 0) ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
    }
}
