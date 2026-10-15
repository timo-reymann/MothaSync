namespace MothaSync.Client.Test;

public sealed class ServerReporterTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("sync.example.com")]
    public async Task TestEnrollmentAsync_UnparseableUrl_ReturnsUnreachable(string serverUrl)
    {
        var result = await ServerReporter.TestEnrollmentAsync(serverUrl, "token");

        Assert.Equal(ServerReporter.EnrollmentCheck.Unreachable, result);
    }

    [Fact]
    public async Task TestEnrollmentAsync_UnreachableHost_ReturnsUnreachable()
    {
        // Nothing listens on this loopback port, so the request fails fast without
        // leaving the machine.
        var result = await ServerReporter.TestEnrollmentAsync("http://127.0.0.1:1", "token");

        Assert.Equal(ServerReporter.EnrollmentCheck.Unreachable, result);
    }
}
