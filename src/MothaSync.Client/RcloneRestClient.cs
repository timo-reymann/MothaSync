using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MothaSync.Client;

internal sealed record RcloneJobStatus(bool Finished, bool Success, string Error);

internal sealed record RcloneStats(
    long Bytes,
    long TotalBytes,
    int Transfers,
    int TotalTransfers,
    int Errors)
{
    public IReadOnlyList<RcloneActiveTransfer> Transferring { get; init; } = [];
}

internal sealed record RcloneActiveTransfer(
    string Name,
    long Size,
    long Bytes,
    int Percentage,
    double? Eta);

internal sealed record RcloneCompletedTransfer(
    string Name,
    long Size,
    long Bytes,
    bool Checked,
    string What,
    long Timestamp,
    string Error);

internal sealed record RcloneTransferredResponse
{
    public IReadOnlyList<RcloneCompletedTransfer> Transferred { get; init; } = [];
}

internal sealed class RcloneRestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public RcloneRestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<long> StartSyncAsync(string sourcePath, string destinationPath, CancellationToken token)
    {
        using var response = await PostAsync("sync/sync", new
        {
            srcFs = sourcePath,
            dstFs = destinationPath,
            _async = true
        }, token);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        return document.RootElement.GetProperty("jobid").GetInt64();
    }

    public async Task<RcloneJobStatus> GetJobStatusAsync(long jobId, CancellationToken token)
    {
        using var response = await PostAsync("job/status", new { jobid = jobId }, token);
        var result = await response.Content.ReadFromJsonAsync<RcloneJobStatus>(JsonOptions, token)
                     ?? throw new InvalidOperationException("rclone returned an empty job status.");
        return result;
    }

    public async Task<RcloneStats> GetStatsAsync(long jobId, CancellationToken token)
    {
        using var response = await PostAsync("core/stats", new { group = $"job/{jobId}" }, token);
        var result = await response.Content.ReadFromJsonAsync<RcloneStats>(JsonOptions, token)
                     ?? throw new InvalidOperationException("rclone returned empty statistics.");
        return result;
    }

    public async Task<IReadOnlyList<RcloneCompletedTransfer>> GetTransferredAsync(long jobId, CancellationToken token)
    {
        using var response = await PostAsync("core/transferred", new { group = $"job/{jobId}" }, token);
        var result = await response.Content.ReadFromJsonAsync<RcloneTransferredResponse>(JsonOptions, token)
                     ?? throw new InvalidOperationException("rclone returned empty transfer history.");
        return result.Transferred;
    }

    private async Task<HttpResponseMessage> PostAsync(string endpoint, object body, CancellationToken token)
    {
        var response = await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions, token);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(token);
            response.Dispose();
            throw new HttpRequestException($"rclone {endpoint} failed ({(int)response.StatusCode}): {message}");
        }

        return response;
    }
}
