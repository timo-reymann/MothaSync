namespace MothaSync.Client.Test;

public sealed class SyncPathsTests
{
    [Fact]
    public void AreSame_IdenticalPaths_ReturnsTrue()
    {
        Assert.True(SyncPaths.AreSame("/mnt/drive", "/mnt/drive"));
    }

    [Fact]
    public void AreSame_IgnoresTrailingSeparatorAndWhitespace()
    {
        Assert.True(SyncPaths.AreSame("/mnt/drive/", " /mnt/drive "));
    }

    [Fact]
    public void AreSame_ResolvesDotSegments()
    {
        Assert.True(SyncPaths.AreSame("/mnt/a/../drive", "/mnt/drive"));
    }

    [Fact]
    public void AreSame_DifferentPaths_ReturnsFalse()
    {
        Assert.False(SyncPaths.AreSame("/mnt/drive-a", "/mnt/drive-b"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("/mnt/drive", "")]
    [InlineData(null, "/mnt/drive")]
    public void AreSame_EmptyPath_ReturnsFalse(string? source, string? destination)
    {
        Assert.False(SyncPaths.AreSame(source, destination));
    }
}
