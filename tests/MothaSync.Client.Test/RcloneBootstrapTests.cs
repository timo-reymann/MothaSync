namespace MothaSync.Client.Test;

public sealed class RcloneBootstrapTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"mothasync-rclone-test-{Guid.NewGuid():N}");

    public RcloneBootstrapTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of temp files.
        }
    }

    private string WriteBundled(string content)
    {
        var path = Path.Combine(_root, "bundled", OperatingSystem.IsWindows() ? "rclone.exe" : "rclone");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string InstalledPath(string name = "installed")
    {
        var path = Path.Combine(_root, "bin", name, OperatingSystem.IsWindows() ? "rclone.exe" : "rclone");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    [Fact]
    public void EnsureInstalled_CopiesBundledBinaryWhenMissing()
    {
        var bundled = WriteBundled("bundled-binary");
        var installed = InstalledPath();

        RcloneBootstrap.EnsureInstalled(bundled, installed);

        Assert.Equal("bundled-binary", File.ReadAllText(installed));
    }

    [Fact]
    public void EnsureInstalled_SkipsCopyWhenHashesMatch()
    {
        var bundled = WriteBundled("same-content");
        var installed = InstalledPath();
        File.WriteAllText(installed, "same-content");
        var originalWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(installed, originalWriteTime.UtcDateTime);

        RcloneBootstrap.EnsureInstalled(bundled, installed);

        Assert.Equal(originalWriteTime.UtcDateTime, File.GetLastWriteTimeUtc(installed));
    }

    [Fact]
    public void EnsureInstalled_OverwritesWhenContentDiffers()
    {
        var bundled = WriteBundled("new-content");
        var installed = InstalledPath();
        File.WriteAllText(installed, "old-content");

        RcloneBootstrap.EnsureInstalled(bundled, installed);

        Assert.Equal("new-content", File.ReadAllText(installed));
    }

    [Fact]
    public void EnsureInstalled_MissingBundledBinary_ThrowsFileNotFound()
    {
        var missing = Path.Combine(_root, "does-not-exist");
        var installed = InstalledPath();

        Assert.Throws<FileNotFoundException>(() => RcloneBootstrap.EnsureInstalled(missing, installed));
        Assert.False(File.Exists(installed));
    }

    [Fact]
    public void EnsureInstalled_MakesBinaryExecutableOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var bundled = WriteBundled("bundled-binary");
        var installed = InstalledPath();

        RcloneBootstrap.EnsureInstalled(bundled, installed);

        var mode = File.GetUnixFileMode(installed);
        Assert.True(mode.HasFlag(UnixFileMode.UserExecute));
        Assert.True(mode.HasFlag(UnixFileMode.GroupExecute));
        Assert.True(mode.HasFlag(UnixFileMode.OtherExecute));
    }
}
