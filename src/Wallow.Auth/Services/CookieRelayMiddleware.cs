namespace Wallow.Auth.Services;

/// <summary>
/// Reads the "cookieRelay" query parameter from incoming requests, retrieves the
/// stored Set-Cookie headers from <see cref="CookieRelayStore"/>, and writes them
/// to the HTTP response so the browser persists them.
/// </summary>
public sealed class CookieRelayMiddleware(RequestDelegate next, CookieRelayStore store)
{
    public async Task InvokeAsync(HttpContext context)
    {
        string? relayKey = context.Request.Query["cookieRelay"];

        if (!string.IsNullOrEmpty(relayKey))
        {
            IReadOnlyList<string>? cookies = store.TryConsume(relayKey);
            if (cookies is not null)
            {
                foreach (string setCookie in cookies)
                {
                    context.Response.Headers.Append("Set-Cookie", setCookie);
                }
            }
        }

        await next(context);
    }
}
