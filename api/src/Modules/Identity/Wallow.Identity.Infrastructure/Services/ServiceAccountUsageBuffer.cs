using System.Collections.Concurrent;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class ServiceAccountUsageBuffer
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);

    public void Record(string clientId)
    {
        _entries[clientId] = DateTimeOffset.UtcNow;
    }

    public Dictionary<string, DateTimeOffset> DrainAll()
    {
        Dictionary<string, DateTimeOffset> snapshot = new(_entries.Count, StringComparer.Ordinal);

        foreach (string key in _entries.Keys)
        {
            if (_entries.TryRemove(key, out DateTimeOffset value))
            {
                snapshot[key] = value;
            }
        }

        return snapshot;
    }
}
