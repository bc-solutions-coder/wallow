using Microsoft.Extensions.Caching.Distributed;
using Wallow.Shared.Kernel.Settings;

namespace Wallow.Shared.Infrastructure.Settings;

public static class SettingsCacheInvalidationHandlers
{
    public static async Task HandleAsync(
        TenantSettingChangedEvent evt,
        IDistributedCache cache,
        CancellationToken ct)
    {
        string cacheKey = $"settings:{evt.ModuleId}:{evt.TenantId.Value}";
        await cache.RemoveAsync(cacheKey, ct);
    }

    public static async Task HandleAsync(
        UserSettingChangedEvent evt,
        IDistributedCache cache,
        CancellationToken ct)
    {
        string cacheKey = $"settings:{evt.ModuleId}:{evt.TenantId.Value}:{evt.UserId.Value}";
        await cache.RemoveAsync(cacheKey, ct);
    }
}
