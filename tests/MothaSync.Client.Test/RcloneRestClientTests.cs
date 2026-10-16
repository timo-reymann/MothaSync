using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace MothaSync.Client.Test;

public sealed class RcloneRestClientTests
{
    [Fact]
    public async Task StartSyncAsync_PostsAsyncJobWithBothPaths()
    {
        using var handler = new RecordingHandler("{\"jobid\":42}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new RcloneRestClient(httpClient);

        var jobId = await client.StartSyncAsync("/source", "/destination", CancellationToken.None);

        Assert.Equal(42, jobId);
        Assert.Equal("sync/sync", handler.Path);
        using var request = JsonDocument.Parse(handler.Body);
        Assert.Equal("/source", request.RootElement.GetProperty("srcFs").GetString());
        Assert.Equal("/destination", request.RootElement.GetProperty("dstFs").GetString());
        Assert.True(request.RootElement.GetProperty("_async").GetBoolean());
    }

    [Fact]
    public async Task GetJobStatusAndStats_DeserializesRcloneResponse()
    {
        using var handler = new RecordingHandler("{\"finished\":true,\"success\":true,\"error\":\"\"}",
            "{\"bytes\":10485760,\"totalBytes\":20971520,\"transfers\":1,\"totalTransfers\":2,\"errors\":0}",
            "{\"transferred\":[]}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new RcloneRestClient(httpClient);

        var status = await client.GetJobStatusAsync(42, CancellationToken.None);
        var stats = await client.GetStatsAsync(42, CancellationToken.None);
        var transferred = await client.GetTransferredAsync(42, CancellationToken.None);

        Assert.True(status.Finished);
        Assert.True(status.Success);
        Assert.Equal(10 * 1024 * 1024, stats.Bytes);
        Assert.Equal(2, stats.TotalTransfers);
        Assert.Empty(transferred);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public RecordingHandler(params string[] responses) => _responses = new Queue<string>(responses);

        public string Path { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? string.Empty;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue())
            };
        }
    }
}
