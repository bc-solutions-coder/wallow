using System.Diagnostics;
using Serilog.Context;

namespace Foundry.Api.Middleware;

internal sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            if (Activity.Current is { } activity)
            {
                activity.SetTag("foundry.correlation_id", correlationId);
            }

            await _next(context);
        }
    }
}
