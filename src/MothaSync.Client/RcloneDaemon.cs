using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MothaSync.Client;

internal sealed class RcloneDaemon : IAsyncDisposable
{
    private readonly Process _process;
    private readonly HttpClient _httpClient;

    private RcloneDaemon(Process process, HttpClient httpClient)
    {
        _process = process;
        _httpClient = httpClient;
        Client = new RcloneRestClient(httpClient);
    }

    public RcloneRestClient Client { get; }

    public static async Task<RcloneDaemon> StartAsync(string executablePath, CancellationToken token)
    {
        var port = GetFreePort();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("rcd");
        process.StartInfo.ArgumentList.Add("--rc-addr");
        process.StartInfo.ArgumentList.Add($"127.0.0.1:{port}");
        process.StartInfo.ArgumentList.Add("--rc-no-auth");
        process.StartInfo.ArgumentList.Add("--log-level");
        process.StartInfo.ArgumentList.Add("NOTICE");

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start rclone.");
            }

            var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
            var daemon = new RcloneDaemon(process, httpClient);
            await daemon.WaitUntilReadyAsync(token);
            return daemon;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private async Task WaitUntilReadyAsync(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "rc/noop");
                using var response = await _httpClient.SendAsync(request, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException) when (!timeout.IsCancellationRequested)
            {
                // The daemon can take a moment to bind its local port.
            }

            await Task.Delay(100, timeout.Token);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
