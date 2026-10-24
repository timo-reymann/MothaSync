using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace MothaSync.Server.Logging;

/// <summary>Writes one log line per request with the controller, action, method, path, status code, and duration.</summary>
/// <remarks>
/// The middleware writes the line at information level for status codes below 400, warning for 4xx,
/// and error for 5xx. A thrown exception is logged with the exception detail and then rethrown.
/// </remarks>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string MessageTemplate =
        "{Method} {Path} -> {StatusCode} in {DurationMs} ms (controller {Controller}, action {Action})";

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? failure = null;
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            LogRequest(context, stopwatch.ElapsedMilliseconds, failure);
        }
    }

    private void LogRequest(HttpContext context, long elapsedMilliseconds, Exception? failure)
    {
        var (controller, action) = Describe(context.GetEndpoint());
        var level = failure is not null ? LogLevel.Error
            : context.Response.StatusCode >= 500 ? LogLevel.Error
            : context.Response.StatusCode >= 400 ? LogLevel.Warning
            : LogLevel.Information;

        logger.Log(
            level,
            default,
            failure,
            MessageTemplate,
            context.Request.Method,
            context.Request.Path.Value,
            failure is null ? context.Response.StatusCode : 500,
            elapsedMilliseconds,
            controller,
            action);
    }

    private static (string Controller, string Action) Describe(Endpoint? endpoint)
    {
        var descriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return descriptor is null
            ? ("-", "-")
            : (descriptor.ControllerName, descriptor.ActionName);
    }
}
