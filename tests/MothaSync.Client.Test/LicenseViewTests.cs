using MothaSync.Client.Views;

namespace MothaSync.Client.Test;

public sealed class LicenseViewTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"mothasync-license-test-{Guid.NewGuid():N}");

    public LicenseViewTests() => Directory.CreateDirectory(_root);

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

    [Fact]
    public void LoadLicenseText_ExistingFileReturnsFileContent()
    {
        var path = Path.Combine(_root, "NOTICE");
        File.WriteAllText(path, "MothaSync");

        Assert.Equal("MothaSync", LicenseView.LoadLicenseText(path));
    }

    [Fact]
    public void LoadLicenseText_MissingFileThrows()
    {
        var path = Path.Combine(_root, "NOTICE");

        Assert.Throws<FileNotFoundException>(() => LicenseView.LoadLicenseText(path));
    }
}
