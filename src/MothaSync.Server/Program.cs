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
            // first scrape.
            app.Services.GetRequiredService<SyncMetrics>();
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
