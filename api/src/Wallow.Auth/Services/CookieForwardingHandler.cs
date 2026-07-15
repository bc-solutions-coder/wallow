using Microsoft.Net.Http.Headers;

namespace Wallow.Auth.Services;

/// <summary>
/// Forwards browser cookies to the API AND captures API response cookies
/// (e.g. partial auth cookies from MFA flow) for replay on subsequent calls
/// within the same Blazor circuit. When HttpContext is unavailable (interactive
/// callback), stores cookies in <see cref="CookieRelayStore"/> so they can be
/// relayed to the browser on the next forceLoad navigation.
/// </summary>
public sealed partial class CookieForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    ApiCookieJar cookieJar,
    CookieRelayStore relayStore,
    ILogger<CookieForwardingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        bool hasHttpContext = httpContext is not null;

        LogOutgoingRequest(request.RequestUri?.PathAndQuery, hasHttpContext);

        // During prerender, HttpContext is available — capture browser cookies into the jar
        // so they survive into the interactive phase where HttpContext is null.
        string? browserCookie = httpContext?.Request.Headers[HeaderNames.Cookie];
        if (!string.IsNullOrEmpty(browserCookie))
        {
            LogSeedingBrowserCookies(browserCookie.Length);
            cookieJar.SeedFromBrowserCookies(browserCookie);
        }

        // Forward X-Forwarded-For so the API rate-limits by real client IP, not Docker network IP
        if (httpContext is not null)
        {
            string? clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(clientIp))
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
            }
        }

        // Forward all cookies from the jar (browser + API response cookies merged)
        string? allCookies = cookieJar.GetCookieHeader();
        if (!string.IsNullOrEmpty(allCookies))
        {
            LogForwardingCookies(allCookies);
            request.Headers.TryAddWithoutValidation("Cookie", allCookies);
        }
        else
        {
            LogNoCookiesToForward();
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        LogApiResponse(request.RequestUri?.PathAndQuery, (int)response.StatusCode);

        // Capture Set-Cookie headers from the API response for future calls
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
        {
            List<string> setCookieList = setCookies.ToList();
            LogApiSetCookieHeaders(setCookieList.Count);

            foreach (string cookie in setCookieList)
            {
                cookieJar.CaptureSetCookie(cookie);
            }

            if (httpContext is not null && !httpContext.Response.HasStarted)
            {
                // Prerender/SSR phase: relay cookies directly to the browser
                LogRelayingCookiesDirectly(setCookieList.Count);
                foreach (string cookie in setCookieList)
                {
                    httpContext.Response.Headers.Append("Set-Cookie", cookie);
                }
            }
            else
            {
                // Interactive callback: HttpContext unavailable or response already started.
                // Park cookies in the relay store so they reach the browser on the next
                // forceLoad navigation (e.g., MFA challenge/enroll redirect).
                LogStoringCookiesInRelay(setCookieList.Count);
                foreach (string cookie in setCookieList)
                {
                    cookieJar.TrackForRelay(cookie);
                }

                IReadOnlyList<string> pending = cookieJar.ConsumePendingRelayHeaders();
                if (pending.Count > 0)
                {
                    cookieJar.PendingRelayKey = relayStore.Store(pending);
                    LogRelayKeyCreated(cookieJar.PendingRelayKey, pending.Count);
                }
            }
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC cookie-fwd: outgoing request to {Path}, hasHttpContext={HasHttpContext}")]
    private partial void LogOutgoingRequest(string? path, bool hasHttpContext);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC cookie-fwd: seeding browser cookies, headerLength={Length}")]
    private partial void LogSeedingBrowserCookies(int length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC cookie-fwd: forwarding cookies to API: {Cookies}")]
    private partial void LogForwardingCookies(string cookies);

    [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC cookie-fwd: no cookies to forward to API")]
    private partial void LogNoCookiesToForward();

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-fwd: API response for {Path}: statusCode={StatusCode}")]
    private partial void LogApiResponse(string? path, int statusCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-fwd: API returned {Count} Set-Cookie header(s)")]
    private partial void LogApiSetCookieHeaders(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-fwd: relaying {Count} cookie(s) directly to browser (prerender/SSR)")]
    private partial void LogRelayingCookiesDirectly(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-fwd: storing {Count} cookie(s) in relay store (interactive callback)")]
    private partial void LogStoringCookiesInRelay(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC cookie-fwd: relay key created: {RelayKey} with {Count} cookie(s)")]
    private partial void LogRelayKeyCreated(string relayKey, int count);
}
