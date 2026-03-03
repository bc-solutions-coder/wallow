using System.Collections.Concurrent;
using System.Text.Json;
using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Configuration.Infrastructure.Services;

public sealed class CachedFeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureFlagService _inner;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);
    private static readonly DistributedCacheEntryOptions _cacheOptions = new() { AbsoluteExpirationRelativeToNow = _cacheTtl };
    private static readonly ConcurrentDictionary<string, ConcurrentBag<string>> _keysByFlag = new();

    public CachedFeatureFlagService(IFeatureFlagService inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string key, Guid tenantId, Guid? userId = null, CancellationToken ct = default)
    {
        string cacheKey = BuildCacheKey("bool", key, tenantId, userId);
        TrackKey(key, cacheKey);
        string? cached = await _cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<bool>(cached);
        }

        bool result = await _inner.IsEnabledAsync(key, tenantId, userId, ct);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), _cacheOptions, ct);
        return result;
    }

    public async Task<string?> GetVariantAsync(string key, Guid tenantId, Guid? userId = null, CancellationToken ct = default)
    {
        string cacheKey = BuildCacheKey("variant", key, tenantId, userId);
        TrackKey(key, cacheKey);
        string? cached = await _cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<string?>(cached);
        }

        string? result = await _inner.GetVariantAsync(key, tenantId, userId, ct);
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), _cacheOptions, ct);
        return result;
    }

    public Task<Dictionary<string, object>> GetAllFlagsAsync(Guid tenantId, Guid? userId = null, CancellationToken ct = default)
        => _inner.GetAllFlagsAsync(tenantId, userId, ct);

    public static async Task InvalidateAsync(IDistributedCache cache, string flagKey)
    {
        if (_keysByFlag.TryRemove(flagKey, out ConcurrentBag<string>? trackedKeys))
        {
            foreach (string cacheKey in trackedKeys.Distinct())
            {
                await cache.RemoveAsync(cacheKey);
            }
        }
    }

    private static void TrackKey(string flagKey, string cacheKey)
    {
        ConcurrentBag<string> keys = _keysByFlag.GetOrAdd(flagKey, _ => []);
        keys.Add(cacheKey);
    }

    private static string BuildCacheKey(string prefix, string flagKey, Guid tenantId, Guid? userId)
        => userId.HasValue
            ? $"ff:{prefix}:{flagKey}:{tenantId}:{userId}"
            : $"ff:{prefix}:{flagKey}:{tenantId}:";
}
