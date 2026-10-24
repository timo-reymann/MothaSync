using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using MothaSync.Server.Auth;
using MothaSync.Server.Data;
using MothaSync.Server.Metrics;
using MothaSync.Server.Protobuf;
using Prometheus;

var app = Program.BuildApplication(Program.CreateWebApplicationBuilder(args));
await app.RunAsync();

public static partial class Program
{
    /// <summary>Creates a builder with server services and the optional metrics listener URL.</summary>
    /// <param name="args">Command-line configuration arguments; null is treated as an empty array.</param>
    /// <remarks>
    /// When Metrics:Enabled is true (the default), appends http://0.0.0.0:{Metrics:Port} to configured URLs.
    /// The metrics port defaults to 9100.
    /// </remarks>
    internal static WebApplicationBuilder CreateWebApplicationBuilder(string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);

        // Add services to the container.

        builder.Services.AddControllers(options =>
        {
            options.InputFormatters.Insert(0, new ProtobufInputFormatter());
            options.OutputFormatters.Insert(0, new ProtobufOutputFormatter());
            options.RespectBrowserAcceptHeader = true;
        });
        builder.Services.AddSingleton<SyncDb>();
        builder.Services.AddSingleton<SyncMetrics>();
        builder.Services.AddAuthentication(DeviceAuthentication.Scheme)
            .AddScheme<AuthenticationSchemeOptions, DeviceAuthenticationHandler>(DeviceAuthentication.Scheme, null);
        builder.Services.AddAuthorization();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        if (builder.Configuration.GetValue("Metrics:Enabled", true))
        {
            var metricsPort = builder.Configuration.GetValue("Metrics:Port", 9100);

            // Append the metrics listener to the configured server addresses. An
            // explicit Kestrel endpoint would drop the URL-based endpoints with a
            // warning, so the URLs are preserved and extended here.
            var urls = builder.Configuration["urls"]?
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            builder.WebHost.UseUrls([.. urls, $"http://0.0.0.0:{metricsPort}"]);
        }

        return builder;
    }

    /// <summary>Builds the server pipeline with controller routes, authentication, and optional metrics.</summary>
    /// <remarks>
    /// When metrics are enabled, serves metrics under /metrics on Metrics:Port (default 9100).
    /// Unmatched paths on that port return 404, and controller routes are inaccessible there.
    /// Enables HTTP request metrics and initializes business metric collectors.
    /// </remarks>
    /// <returns>The configured application, which has not been started.</returns>
    internal static WebApplication BuildApplication(WebApplicationBuilder builder)
    {
        var app = builder.Build();

        var metricsEnabled = app.Configuration.GetValue("Metrics:Enabled", true);

        if (metricsEnabled)
        {
            var metricsPort = app.Configuration.GetValue("Metrics:Port", 9100);

            // Serve the Prometheus scraper on its own port. The branch is terminal
            // (MapWhen), so neither the API endpoints nor the scrape endpoint leak to
            // the other port.
            app.MapWhen(
                context => context.Connection.LocalPort == metricsPort,
                branch =>
                {
                    branch.UseMetricServer("/metrics");
                    branch.Run(context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return Task.CompletedTask;
                    });
                });

            app.UseHttpMetrics();
            // Create the business metric collectors eagerly so they appear in the
            // first scrape, and restore the persisted per-device state so the
            // inactivity query on the last-run gauges keeps working after a restart.
            var metrics = app.Services.GetRequiredService<SyncMetrics>();
            var db = app.Services.GetRequiredService<SyncDb>();
            foreach (var (deviceId, hostname, registrationTime, lastRun) in db.ListDeviceRunStates())
            {
                metrics.DeviceRestored(deviceId, hostname, lastRun, registrationTime);
            }
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
