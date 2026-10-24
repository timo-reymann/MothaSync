using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using MothaSync.Server.Logging;

namespace MothaSync.Server.Test;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task Invoke_ControllerEndpoint_LogsControllerActionMethodPathStatusAndDuration()
    {
        var logger = new RecordingLogger();
        var middleware = new RequestLoggingMiddleware(FinishWith(200), logger);

        await middleware.InvokeAsync(RequestContext("POST", "/api/runs", RunsEndpoint()));

        var (level, message, _) = Assert.Single(logger.Records);
        Assert.Equal(LogLevel.Information, level);
        Assert.StartsWith("POST /api/runs -> 200 in ", message);
        Assert.EndsWith(" ms (controller Runs, action Report)", message);
    }

    [Fact]
    public async Task Invoke_NonControllerEndpoint_LogsUnknownControllerAndAction()
    {
        var logger = new RecordingLogger();
        var endpoint = new RouteEndpoint(
            static _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/unknown"),
            0,
            new EndpointMetadataCollection(),
            "name");
        var middleware = new RequestLoggingMiddleware(FinishWith(404), logger);

        await middleware.InvokeAsync(RequestContext("GET", "/unknown", endpoint));

        var (level, message, _) = Assert.Single(logger.Records);
        Assert.Equal(LogLevel.Warning, level);
        Assert.StartsWith("GET /unknown -> 404 in ", message);
        Assert.EndsWith(" ms (controller -, action -)", message);
    }

    [Theory]
    [InlineData(307, LogLevel.Information)]
    [InlineData(400, LogLevel.Warning)]
    [InlineData(401, LogLevel.Warning)]
    [InlineData(500, LogLevel.Error)]
    public async Task Invoke_StatusCodes_MapToLogLevels(int statusCode, LogLevel expected)
    {
        var logger = new RecordingLogger();
        var middleware = new RequestLoggingMiddleware(FinishWith(statusCode), logger);

        await middleware.InvokeAsync(RequestContext("POST", "/api/runs", RunsEndpoint()));

        var (level, _, _) = Assert.Single(logger.Records);
        Assert.Equal(expected, level);
    }

    [Fact]
    public async Task Invoke_ThrownException_LogsErrorWithStatus500ThenRethrows()
    {
        var logger = new RecordingLogger();
        var failure = new InvalidOperationException("boom");
        RequestDelegate next = _ => throw failure;
        var middleware = new RequestLoggingMiddleware(next, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(RequestContext("POST", "/api/runs", RunsEndpoint())));

        var record = Assert.Single(logger.Records);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.StartsWith("POST /api/runs -> 500 in ", record.Message);
        Assert.Same(failure, record.Exception);
    }

    [Fact]
    public async Task Invoke_LogsPositiveDuration()
    {
        var logger = new RecordingLogger();
        var middleware = new RequestLoggingMiddleware(FinishWith(200), logger);

        await middleware.InvokeAsync(RequestContext("GET", "/api/runs", RunsEndpoint()));

        var record = Assert.Single(logger.Records);
        var duration = double.Parse(
            record.Message["GET /api/runs -> 200 in ".Length..record.Message.IndexOf(" ms", StringComparison.Ordinal)],
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(duration >= 0);
    }

    private static RequestDelegate FinishWith(int statusCode) =>
        context =>
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        };

    private static DefaultHttpContext RequestContext(string method, string path, Endpoint endpoint)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.SetEndpoint(endpoint);
        return context;
    }

    private static Endpoint RunsEndpoint()
    {
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = "Runs",
            ActionName = "Report",
        };
        return new Endpoint(
            null,
            new EndpointMetadataCollection(descriptor),
            "Runs.Report");
    }

    private sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger : ILogger<RequestLoggingMiddleware>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Records.Add(new LogRecord(logLevel, formatter(state, exception), exception));
    }
}
