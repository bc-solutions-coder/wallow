using System.Collections.Concurrent;

namespace Wallow.Auth.Services;

/// <summary>
/// Scoped per Blazor circuit — stores raw cookie name=value pairs from API responses
/// (e.g. partial auth cookies from MFA flow) for replay on subsequent API calls.
/// Uses raw strings instead of CookieContainer to avoid parsing issues with
/// SameSite and other modern cookie attributes.
/// </summary>
public sealed class ApiCookieJar
{
    private readonly ConcurrentDictionary<string, string> _cookies = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _pendingSetCookieHeaders = [];

    /// <summary>
    /// Relay key for cookies stored in <see cref="CookieRelayStore"/> when HttpContext
    /// was unavailable (interactive callback). Components read this to include the key
    /// in forceLoad navigations so the browser receives the cookies.
    /// </summary>
    public string? PendingRelayKey { get; set; }

    /// <summary>
    /// Extracts the name=value from a Set-Cookie header and stores it.
    /// </summary>
    public void CaptureSetCookie(string setCookieHeader)
    {
        // Set-Cookie format: name=value; path=/; httponly; samesite=lax
        // We only need the "name=value" portion
        int semiIndex = setCookieHeader.IndexOf(';', StringComparison.Ordinal);
        string nameValue = semiIndex >= 0 ? setCookieHeader[..semiIndex] : setCookieHeader;

        int eqIndex = nameValue.IndexOf('=', StringComparison.Ordinal);
        if (eqIndex > 0)
        {
            string name = nameValue[..eqIndex].Trim();
            _cookies[name] = nameValue.Trim();
        }
    }

    /// <summary>
    /// Tracks the full Set-Cookie header for relay to the browser via <see cref="CookieRelayStore"/>.
    /// </summary>
    public void TrackForRelay(string setCookieHeader)
    {
        _pendingSetCookieHeaders.Add(setCookieHeader);
    }

    /// <summary>
    /// Returns and clears all pending Set-Cookie headers accumulated for relay.
    /// </summary>
    public IReadOnlyList<string> ConsumePendingRelayHeaders()
    {
        if (_pendingSetCookieHeaders.Count == 0)
        {
            return [];
        }

        List<string> result = [.. _pendingSetCookieHeaders];
        _pendingSetCookieHeaders.Clear();
        return result;
    }

    /// <summary>
    /// Seeds the jar with browser cookies from the initial HTTP request (prerender phase).
    /// Parses a raw Cookie header value like "name1=val1; name2=val2" into individual entries.
    /// Does not overwrite cookies already captured from API responses.
    /// </summary>
    public void SeedFromBrowserCookies(string cookieHeader)
    {
        foreach (string segment in cookieHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int eqIndex = segment.IndexOf('=', StringComparison.Ordinal);
            if (eqIndex > 0)
            {
                string name = segment[..eqIndex].Trim();
                _cookies.TryAdd(name, segment.Trim());
            }
        }
    }

    /// <summary>
    /// Returns all captured cookies as a Cookie header value (e.g. "name1=val1; name2=val2").
    /// </summary>
    public string? GetCookieHeader()
    {
        if (_cookies.IsEmpty)
        {
            return null;
        }

        return string.Join("; ", _cookies.Values);
    }
}
