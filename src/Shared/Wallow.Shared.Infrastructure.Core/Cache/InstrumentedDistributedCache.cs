using System.Diagnostics;
using System.Diagnostics.Metrics;
using Wallow.Shared.Kernel;
using Microsoft.Extensions.Caching.Distributed;

namespace Wallow.Shared.Infrastructure.Core.Cache;

public sealed class InstrumentedDistributedCache : IDistributedCache
{
    private static readonly Meter _cacheMeter = Diagnostics.CreateMeter("Cache");

    private static readonly Counter<long> _hitsTotal = _cacheMeter.CreateCounter<long>(
        "wallow.cache.hits_total",
        description: "Total number of cache hits");

    private static readonly Counter<long> _missesTotal = _cacheMeter.CreateCounter<long>(
        "wallow.cache.misses_total",
        description: "Total number of cache misses");

    private readonly IDistributedCache _inner;

    public InstrumentedDistributedCache(IDistributedCache inner)
    {
        _inner = inner;
    }

    public byte[]? Get(string key)
    {
        byte[]? value = _inner.Get(key);
        RecordHitOrMiss(key, value is not null);
        return value;
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        byte[]? value = await _inner.GetAsync(key, token);
        RecordHitOrMiss(key, value is not null);
        return value;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        _inner.Set(key, value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default) =>
        _inner.SetAsync(key, value, options, token);

    public void Refresh(string key) => _inner.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        _inner.RefreshAsync(key, token);

    public void Remove(string key) => _inner.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        _inner.RemoveAsync(key, token);

    private static void RecordHitOrMiss(string key, bool isHit)
    {
        TagList tags = new TagList { { "cache_key_prefix", ExtractPrefix(key) } };

        if (isHit)
        {
            _hitsTotal.Add(1, tags);
        }
        else
        {
            _missesTotal.Add(1, tags);
        }
    }

    private static string ExtractPrefix(string key)
    {
        int separatorIndex = key.IndexOf(':', StringComparison.Ordinal);
        return separatorIndex > 0 ? key[..separatorIndex] : key;
    }
}
