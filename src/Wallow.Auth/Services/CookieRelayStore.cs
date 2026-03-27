using System.Collections.Concurrent;

namespace Wallow.Auth.Services;

/// <summary>
/// Singleton store that bridges API Set-Cookie headers across Blazor circuits.
/// When an API response sets cookies during an interactive callback (HttpContext is null),
/// the cookies are parked here under a short-lived relay key. The next HTTP request
/// (triggered by forceLoad navigation) picks them up via CookieRelayMiddleware and
/// writes them to the browser.
/// </summary>
public sealed class CookieRelayStore
{
    private static readonly TimeSpan _expiry = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (IReadOnlyList<string> SetCookieHeaders, DateTime CreatedAt)> _entries = new();

    public string Store(IReadOnlyList<string> setCookieHeaders)
    {
        string key = Guid.NewGuid().ToString("N");
        _entries[key] = (setCookieHeaders, DateTime.UtcNow);
        Cleanup();
        return key;
    }

    public IReadOnlyList<string>? TryConsume(string key)
    {
        if (_entries.TryRemove(key, out (IReadOnlyList<string> SetCookieHeaders, DateTime CreatedAt) entry)
            && DateTime.UtcNow - entry.CreatedAt < _expiry)
        {
            return entry.SetCookieHeaders;
        }

        return null;
    }

    private void Cleanup()
    {
        DateTime cutoff = DateTime.UtcNow - _expiry;
        foreach (KeyValuePair<string, (IReadOnlyList<string> SetCookieHeaders, DateTime CreatedAt)> kvp in _entries)
        {
            if (kvp.Value.CreatedAt < cutoff)
            {
                _entries.TryRemove(kvp.Key, out _);
            }
        }
    }
}
