using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Infrastructure.Middleware;

public sealed partial class ServiceAccountTrackingMiddleware(
    RequestDelegate next,
    ILogger<ServiceAccountTrackingMiddleware> logger,
    ServiceAccountUsageBuffer buffer)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            string? clientId = context.User.FindFirst("azp")?.Value;
            if (clientId?.StartsWith("sa-", StringComparison.Ordinal) == true
                    || clientId?.StartsWith("app-", StringComparison.Ordinal) == true)
            {
                buffer.Record(clientId);
            }
        }
    }
}

public sealed partial class ServiceAccountTrackingMiddleware
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to update LastUsedAt for service account {ClientId}")]
    private partial void LogUpdateLastUsedFailed(Exception ex, string clientId);
}

public static class ServiceAccountTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseServiceAccountTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ServiceAccountTrackingMiddleware>();
    }
}
