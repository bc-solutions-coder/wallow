using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;

namespace Wallow.Identity.Infrastructure.Middleware;

public sealed class SessionRevocationMiddleware(
    RequestDelegate next,
    IConnectionMultiplexer connectionMultiplexer)
{
    private const string CookieName = "wallow.session";
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.Request.Cookies.TryGetValue(CookieName, out string? token)
            || string.IsNullOrEmpty(token))
        {
            await next(context);
            return;
        }

        IDatabase db = connectionMultiplexer.GetDatabase();
        bool isRevoked = await db.KeyExistsAsync($"session:revoked:{token}");

        if (isRevoked)
        {
            context.Response.Cookies.Delete(CookieName);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, new { error = "session_revoked" }, _jsonOptions);
            return;
        }

        await next(context);
    }
}

public static class SessionRevocationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionRevocation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionRevocationMiddleware>();
    }
}
