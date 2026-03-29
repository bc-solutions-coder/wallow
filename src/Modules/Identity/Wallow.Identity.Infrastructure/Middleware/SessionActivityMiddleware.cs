using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Middleware;

public sealed class SessionActivityMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer connectionMultiplexer,
    ISessionService sessionService)
{
    private const string CookieName = "wallow.session";
    private const string ThrottleKeyPrefix = "session:touched:";
    private static readonly TimeSpan _throttleTtl = TimeSpan.FromSeconds(60);

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.User.Identity?.IsAuthenticated != true
            || !context.Request.Cookies.TryGetValue(CookieName, out string? token)
            || string.IsNullOrEmpty(token))
        {
            return;
        }

        IDatabase redis = connectionMultiplexer.GetDatabase();
        bool wasSet = await redis.StringSetAsync(
            $"{ThrottleKeyPrefix}{token}",
            "1",
            _throttleTtl,
            false,
            When.NotExists);

        if (wasSet)
        {
            // Fire-and-forget DB update of last_activity_at
            _ = sessionService.TouchSessionAsync(token, CancellationToken.None);
        }
    }
}

public static class SessionActivityMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionActivity(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionActivityMiddleware>();
    }
}
