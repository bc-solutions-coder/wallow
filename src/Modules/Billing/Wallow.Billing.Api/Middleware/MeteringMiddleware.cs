using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Wallow.Billing.Application.Metering.Commands.IncrementMeter;
using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Application.Metering.Services;
using Wallow.Billing.Domain.Metering.Enums;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Billing.Api.Middleware;

/// <summary>
/// Middleware that checks quotas before requests and increments counters after successful responses.
/// Only tracks API routes (/api/*). Caches quota lookups per tenant to reduce DB hits.
/// </summary>
public sealed class MeteringMiddleware(RequestDelegate next, HybridCache cache)
{
    private const string ApiCallsMeterCode = "api.calls";
    private static readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };

    public async Task InvokeAsync(HttpContext context, IMeteringService meteringService, ITenantContext tenantContext, IMessageBus messageBus)
    {
        // Skip non-API routes
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        // Check quota with caching by tenant ID to avoid per-request DB lookups
        string cacheKey = $"quota:{tenantContext.TenantId.Value}:{ApiCallsMeterCode}";
        QuotaCheckResult quotaCheck = await cache.GetOrCreateAsync(cacheKey,
            _ => new ValueTask<QuotaCheckResult>(meteringService.CheckQuotaAsync(ApiCallsMeterCode)),
            _cacheOptions);

        if (!quotaCheck.IsAllowed && quotaCheck.ActionIfExceeded == QuotaAction.Block)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["X-RateLimit-Limit"] = quotaCheck.Limit.ToString("F0");
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            context.Response.Headers["Retry-After"] = GetSecondsUntilReset().ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Quota exceeded",
                limit = quotaCheck.Limit,
                currentUsage = quotaCheck.CurrentUsage,
                percentUsed = quotaCheck.PercentUsed
            });
            return;
        }

        // Add warning header if approaching limit (>80%)
        if (quotaCheck.PercentUsed > 80)
        {
            context.Response.Headers["X-Quota-Warning"] = $"{quotaCheck.PercentUsed:F0}% used";
        }

        // Add rate limit headers
        if (quotaCheck.Limit < decimal.MaxValue)
        {
            decimal remaining = Math.Max(0, quotaCheck.Limit - quotaCheck.CurrentUsage - 1);
            context.Response.Headers["X-RateLimit-Limit"] = quotaCheck.Limit.ToString("F0");
            context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString("F0");
            context.Response.Headers["X-RateLimit-Reset"] = GetResetTimestamp().ToString();
        }

        // Process the request
        await next(context);

        // Only count successful requests (status < 400)
        // Dispatch via Wolverine so the handler runs in its own DI scope
        if (context.Response.StatusCode < 400)
        {
            await messageBus.SendAsync(new IncrementMeterCommand(ApiCallsMeterCode));
        }
    }

    private static int GetSecondsUntilReset()
    {
        DateTime now = DateTime.UtcNow;
        DateTime nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        return (int)(nextMonth - now).TotalSeconds;
    }

    private static long GetResetTimestamp()
    {
        DateTime now = DateTime.UtcNow;
        DateTime nextMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        return new DateTimeOffset(nextMonth).ToUnixTimeSeconds();
    }

}
