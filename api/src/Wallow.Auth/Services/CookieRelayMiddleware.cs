namespace Wallow.Auth.Services;

/// <summary>
/// Reads the "cookieRelay" query parameter from incoming requests, retrieves the
/// stored Set-Cookie headers from <see cref="CookieRelayStore"/>, and writes them
/// to the HTTP response so the browser persists them.
/// </summary>
public sealed partial class CookieRelayMiddleware(RequestDelegate next, CookieRelayStore store, ILogger<CookieRelayMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string? relayKey = context.Request.Query["cookieRelay"];

        if (!string.IsNullOrEmpty(relayKey))
        {
            string requestPath = context.Request.Path;
            LogRelayKeyFound(relayKey, requestPath);

            IReadOnlyList<string>? cookies = store.TryConsume(relayKey);
            if (cookies is not null)
            {
                LogRelayingCookiesToBrowser(relayKey, cookies.Count);
                foreach (string setCookie in cookies)
                {
                    context.Response.Headers.Append("Set-Cookie", setCookie);
                }
            }
            else
            {
                LogRelayKeyNotFoundOrExpired(relayKey);
            }
        }

        await next(context);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-relay: found relay key={RelayKey} on request to {Path}")]
    private partial void LogRelayKeyFound(string relayKey, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-relay: writing {Count} cookie(s) to browser for key={RelayKey}")]
    private partial void LogRelayingCookiesToBrowser(string relayKey, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC cookie-relay: key={RelayKey} not found or expired in store")]
    private partial void LogRelayKeyNotFoundOrExpired(string relayKey);
}
