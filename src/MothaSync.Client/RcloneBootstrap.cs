using System;
using System.IO;
using System.Security.Cryptography;

namespace MothaSync.Client;

public static class RcloneBootstrap
{
    public static string InstalledPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MothaSync",
        "bin",
        OperatingSystem.IsWindows() ? "rclone.exe" : "rclone");

    public static void EnsureInstalled()
    {
        var bundledPath = Path.Combine(
            AppContext.BaseDirectory,
            "rclone",
            OperatingSystem.IsWindows() ? "rclone.exe" : "rclone");

        if (!File.Exists(bundledPath))
        {
            return;
        }

        try
        {
            EnsureInstalled(bundledPath, InstalledPath);
        }
        catch (IOException)
        {
            // Leave the existing copy in place; the sync run will surface any problem.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above when the destination is not writable.
        }
    }

    internal static void EnsureInstalled(string bundledPath, string installedPath)
    {
        if (File.Exists(installedPath) && HashesMatch(bundledPath, installedPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(installedPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(bundledPath, installedPath, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(installedPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static bool HashesMatch(string bundledPath, string installedPath)
    {
        var bundled = SHA256.HashData(File.ReadAllBytes(bundledPath));
        var installed = SHA256.HashData(File.ReadAllBytes(installedPath));
        return bundled.AsSpan().SequenceEqual(installed);
    }
}
