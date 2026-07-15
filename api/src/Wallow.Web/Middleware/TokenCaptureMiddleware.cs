using Microsoft.AspNetCore.Authentication;
using Wallow.Web.Services;

namespace Wallow.Web.Middleware;

/// <summary>
/// Captures the OIDC access token from the initial HTTP request into a scoped TokenProvider.
/// This makes the token available inside the Blazor Server SignalR circuit where
/// HttpContext is null.
/// </summary>
public sealed partial class TokenCaptureMiddleware(RequestDelegate next, ILogger<TokenCaptureMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, TokenProvider tokenProvider)
    {
        string path = context.Request.Path;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            LogTokenCaptureBegin(path);

            string? token = await context.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
            {
                tokenProvider.AccessToken = token;
                LogTokenCaptureSuccess(path);
            }
            else
            {
                LogNoAccessTokenAvailable(path);
            }
        }

        await next(context);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Beginning token capture for {Path}")]
    internal partial void LogTokenCaptureBegin(string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Completed token capture for {Path}")]
    internal partial void LogTokenCaptureSuccess(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authenticated request has no access token for {Path}")]
    internal partial void LogNoAccessTokenAvailable(string path);
}
