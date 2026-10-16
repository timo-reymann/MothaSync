using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MothaSync.Server.Test;

/// <summary>End-to-end tests for the metrics HTTP server on its dedicated port.</summary>
public sealed class MetricsEndpointTests
{
    [Fact]
    public async Task MetricsPort_ServesScrapeOnly_ApiPort_ServesApiOnly()
    {
        var apiPort = GetFreePort();
        var metricsPort = GetFreePort();
        var dbPath = Path.Combine(Path.GetTempPath(), $"mothasync-test-{Guid.NewGuid():N}.db");

        // Both ports must be part of configuration before the server binds, so they
        // are passed through the environment (double-underscore form).
        Environment.SetEnvironmentVariable("Metrics__Port", metricsPort.ToString());
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://127.0.0.1:{apiPort}");
        try
        {
            var builder = Program.CreateWebApplicationBuilder([]);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:DatabasePath"] = dbPath,
            });

            using var app = Program.BuildApplication(builder);
            await app.StartAsync();
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var scrape = await client.GetStringAsync($"http://127.0.0.1:{metricsPort}/metrics");
                Assert.Contains("mothasync_devices_registered_total", scrape);
                Assert.Contains("http_requests_received_total", scrape);
                Assert.Contains("http_request_duration_seconds", scrape);
                Assert.Contains("process_cpu_seconds_total", scrape);
                Assert.Contains("dotnet_", scrape);

                // API endpoints are not reachable through the metrics port.
                var apiOnMetricsPort = await client.GetAsync($"http://127.0.0.1:{metricsPort}/api/devices/verify-enrollment");
                Assert.Equal(HttpStatusCode.NotFound, apiOnMetricsPort.StatusCode);

                // The scrape endpoint is not reachable through the API port.
                var metricsOnApiPort = await client.GetAsync($"http://127.0.0.1:{apiPort}/metrics");
                Assert.Equal(HttpStatusCode.NotFound, metricsOnApiPort.StatusCode);
            }
            finally
            {
                await app.StopAsync();
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("Metrics__Port", null);
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);
            File.Delete(dbPath);
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
