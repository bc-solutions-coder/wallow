using Microsoft.AspNetCore.Authentication;
using Wallow.Web.Services;

namespace Wallow.Web.Middleware;

/// <summary>
/// Captures the OIDC access token from the initial HTTP request into a scoped TokenProvider.
/// This makes the token available inside the Blazor Server SignalR circuit where
/// HttpContext is null.
/// </summary>
public sealed class TokenCaptureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TokenProvider tokenProvider)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? token = await context.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                tokenProvider.AccessToken = token;
            }
        }

        await next(context);
    }
}
