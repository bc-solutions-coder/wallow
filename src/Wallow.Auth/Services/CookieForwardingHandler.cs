using Microsoft.Net.Http.Headers;

namespace Wallow.Auth.Services;

/// <summary>
/// Forwards browser cookies to the API AND captures API response cookies
/// (e.g. partial auth cookies from MFA flow) for replay on subsequent calls
/// within the same Blazor circuit. When HttpContext is unavailable (interactive
/// callback), stores cookies in <see cref="CookieRelayStore"/> so they can be
/// relayed to the browser on the next forceLoad navigation.
/// </summary>
public sealed class CookieForwardingHandler(
    IHttpContextAccessor httpContextAccessor,
    ApiCookieJar cookieJar,
    CookieRelayStore relayStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        // During prerender, HttpContext is available — capture browser cookies into the jar
        // so they survive into the interactive phase where HttpContext is null.
        string? browserCookie = httpContext?.Request.Headers[HeaderNames.Cookie];
        if (!string.IsNullOrEmpty(browserCookie))
        {
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
            request.Headers.TryAddWithoutValidation("Cookie", allCookies);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // Capture Set-Cookie headers from the API response for future calls
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
        {
            List<string> setCookieList = setCookies.ToList();

            foreach (string cookie in setCookieList)
            {
                cookieJar.CaptureSetCookie(cookie);
            }

            if (httpContext is not null && !httpContext.Response.HasStarted)
            {
                // Prerender/SSR phase: relay cookies directly to the browser
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
                foreach (string cookie in setCookieList)
                {
                    cookieJar.TrackForRelay(cookie);
                }

                IReadOnlyList<string> pending = cookieJar.ConsumePendingRelayHeaders();
                if (pending.Count > 0)
                {
                    cookieJar.PendingRelayKey = relayStore.Store(pending);
                }
            }
        }

        return response;
    }
}
