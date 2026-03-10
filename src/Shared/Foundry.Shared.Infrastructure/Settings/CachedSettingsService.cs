using System.Text.Json;
using Foundry.Shared.Infrastructure.Core.Persistence;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.Settings;
using Microsoft.Extensions.Caching.Distributed;

namespace Foundry.Shared.Infrastructure.Settings;

public sealed class CachedSettingsService<TDbContext>(
    ITenantSettingRepository<TDbContext> tenantRepo,
    IUserSettingRepository<TDbContext> userRepo,
    ISettingRegistry registry,
    IDistributedCache cache) : ISettingsService
    where TDbContext : TenantAwareDbContext<TDbContext>
{
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    private static readonly DistributedCacheEntryOptions _cacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = _cacheTtl
    };

    // ── Read ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ResolvedSetting>> GetTenantSettingsAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        string cacheKey = TenantCacheKey(tenantId);
        string? cached = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<ResolvedSetting>>(cached)!;
        }

        TenantId tid = TenantId.Create(tenantId);
        IReadOnlyList<TenantSettingEntity> tenantSettings =
            await tenantRepo.GetAllAsync(tid, registry.ModuleName, ct);

        List<ResolvedSetting> resolved = MergeTenantWithDefaults(tenantSettings);
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(resolved), _cacheOptions, ct);
        return resolved;
    }

    public async Task<IReadOnlyList<ResolvedSetting>> GetUserSettingsAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        string cacheKey = UserCacheKey(tenantId, userId);
        string? cached = await cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<ResolvedSetting>>(cached)!;
        }

        TenantId tid = TenantId.Create(tenantId);
        IReadOnlyList<TenantSettingEntity> tenantSettings =
            await tenantRepo.GetAllAsync(tid, registry.ModuleName, ct);
        IReadOnlyList<UserSettingEntity> userSettings =
            await userRepo.GetAllAsync(tid, userId.ToString("D", System.Globalization.CultureInfo.InvariantCulture), registry.ModuleName, ct);

        List<ResolvedSetting> resolved = MergeAllWithDefaults(tenantSettings, userSettings);
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(resolved), _cacheOptions, ct);
        return resolved;
    }

    public async Task<ResolvedSettingsConfig> GetConfigAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        IReadOnlyList<ResolvedSetting> settings = await GetUserSettingsAsync(tenantId, userId, ct);
        Dictionary<string, string> dict = settings.ToDictionary(s => s.Key, s => s.Value);
        return new ResolvedSettingsConfig(dict);
    }

    // ── Write ───────────────────────────────────────────────────────────

    public async Task UpdateTenantSettingsAsync(
        Guid tenantId, IReadOnlyList<SettingUpdate> settings, Guid updatedBy, CancellationToken ct = default)
    {
        TenantId tid = TenantId.Create(tenantId);

        foreach (SettingUpdate update in settings)
        {
            ValidateKey(update.Key);
            TenantSettingEntity entity = new(tid, registry.ModuleName, update.Key, update.Value);
            await tenantRepo.UpsertAsync(entity, ct);
        }

        await InvalidateTenantCacheAsync(tenantId, ct);
    }

    public async Task UpdateUserSettingsAsync(
        Guid tenantId, Guid userId, IReadOnlyList<SettingUpdate> settings, CancellationToken ct = default)
    {
        TenantId tid = TenantId.Create(tenantId);
        string userIdStr = userId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        foreach (SettingUpdate update in settings)
        {
            ValidateKey(update.Key);
            UserSettingEntity entity = new(tid, userIdStr, registry.ModuleName, update.Key, update.Value);
            await userRepo.UpsertAsync(entity, ct);
        }

        await InvalidateUserCacheAsync(tenantId, userId, ct);
    }

    // ── Delete ──────────────────────────────────────────────────────────

    public async Task DeleteTenantSettingsAsync(
        Guid tenantId, IReadOnlyList<string> keys, Guid deletedBy, CancellationToken ct = default)
    {
        TenantId tid = TenantId.Create(tenantId);

        foreach (string key in keys)
        {
            await tenantRepo.DeleteAsync(tid, registry.ModuleName, key, ct);
        }

        await InvalidateTenantCacheAsync(tenantId, ct);
    }

    public async Task DeleteUserSettingsAsync(
        Guid tenantId, Guid userId, IReadOnlyList<string> keys, CancellationToken ct = default)
    {
        TenantId tid = TenantId.Create(tenantId);
        string userIdStr = userId.ToString("D", System.Globalization.CultureInfo.InvariantCulture);

        foreach (string key in keys)
        {
            await userRepo.DeleteAsync(tid, userIdStr, registry.ModuleName, key, ct);
        }

        await InvalidateUserCacheAsync(tenantId, userId, ct);
    }

    // ── Merge logic ─────────────────────────────────────────────────────

    private List<ResolvedSetting> MergeTenantWithDefaults(
        IReadOnlyList<TenantSettingEntity> tenantSettings)
    {
        Dictionary<string, TenantSettingEntity> tenantMap = tenantSettings.ToDictionary(s => s.SettingKey);
        Dictionary<string, ResolvedSetting> result = new();

        // Start with code defaults
        foreach (KeyValuePair<string, SettingMetadata> kvp in registry.Metadata)
        {
            string defaultVal = Convert.ToString(kvp.Value.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            bool hasTenant = tenantMap.TryGetValue(kvp.Key, out TenantSettingEntity? tenantEntity);

            result[kvp.Key] = new ResolvedSetting(
                Key: kvp.Key,
                Value: hasTenant ? tenantEntity!.Value : defaultVal,
                Source: hasTenant ? "tenant" : "default",
                DisplayName: kvp.Value.DisplayName,
                Description: kvp.Value.Description,
                DefaultValue: defaultVal);
        }

        // Add tenant-only settings (custom/system keys not in registry)
        foreach (TenantSettingEntity entity in tenantSettings)
        {
            if (!result.ContainsKey(entity.SettingKey))
            {
                result[entity.SettingKey] = new ResolvedSetting(
                    Key: entity.SettingKey,
                    Value: entity.Value,
                    Source: "tenant",
                    DisplayName: null,
                    Description: null,
                    DefaultValue: null);
            }
        }

        return result.Values.ToList();
    }

    private List<ResolvedSetting> MergeAllWithDefaults(
        IReadOnlyList<TenantSettingEntity> tenantSettings,
        IReadOnlyList<UserSettingEntity> userSettings)
    {
        Dictionary<string, TenantSettingEntity> tenantMap = tenantSettings.ToDictionary(s => s.SettingKey);
        Dictionary<string, UserSettingEntity> userMap = userSettings.ToDictionary(s => s.SettingKey);
        Dictionary<string, ResolvedSetting> result = new();

        // Start with code defaults, overlay tenant, overlay user
        foreach (KeyValuePair<string, SettingMetadata> kvp in registry.Metadata)
        {
            string defaultVal = Convert.ToString(kvp.Value.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            bool hasTenant = tenantMap.TryGetValue(kvp.Key, out TenantSettingEntity? tenantEntity);
            bool hasUser = userMap.TryGetValue(kvp.Key, out UserSettingEntity? userEntity);

            string value = defaultVal;
            string source = "default";

            if (hasTenant)
            {
                value = tenantEntity!.Value;
                source = "tenant";
            }

            if (hasUser)
            {
                value = userEntity!.Value;
                source = "user";
            }

            result[kvp.Key] = new ResolvedSetting(
                Key: kvp.Key,
                Value: value,
                Source: source,
                DisplayName: kvp.Value.DisplayName,
                Description: kvp.Value.Description,
                DefaultValue: defaultVal);
        }

        // Add tenant-only and user-only settings not in registry
        foreach (TenantSettingEntity entity in tenantSettings)
        {
            if (!result.ContainsKey(entity.SettingKey))
            {
                bool hasUser = userMap.TryGetValue(entity.SettingKey, out UserSettingEntity? userEntity);

                result[entity.SettingKey] = new ResolvedSetting(
                    Key: entity.SettingKey,
                    Value: hasUser ? userEntity!.Value : entity.Value,
                    Source: hasUser ? "user" : "tenant",
                    DisplayName: null,
                    Description: null,
                    DefaultValue: null);
            }
        }

        foreach (UserSettingEntity entity in userSettings)
        {
            if (!result.ContainsKey(entity.SettingKey))
            {
                result[entity.SettingKey] = new ResolvedSetting(
                    Key: entity.SettingKey,
                    Value: entity.Value,
                    Source: "user",
                    DisplayName: null,
                    Description: null,
                    DefaultValue: null);
            }
        }

        return result.Values.ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void ValidateKey(string key)
    {
        SettingKeyValidationResult result = SettingKeyValidator.Validate(key, registry);

        if (result == SettingKeyValidationResult.Unknown)
        {
            throw new ArgumentException($"Unknown setting key: '{key}'.", nameof(key));
        }
    }

    private async Task InvalidateTenantCacheAsync(Guid tenantId, CancellationToken ct)
    {
        await cache.RemoveAsync(TenantCacheKey(tenantId), ct);
    }

    private async Task InvalidateUserCacheAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        await cache.RemoveAsync(UserCacheKey(tenantId, userId), ct);
    }

    private string TenantCacheKey(Guid tenantId) =>
        $"settings:{registry.ModuleName}:{tenantId}";

    private string UserCacheKey(Guid tenantId, Guid userId) =>
        $"settings:{registry.ModuleName}:{tenantId}:{userId}";
}
