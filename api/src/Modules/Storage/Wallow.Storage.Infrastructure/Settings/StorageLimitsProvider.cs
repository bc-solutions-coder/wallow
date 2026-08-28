using System.Globalization;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Settings;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Settings;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Storage.Infrastructure.Settings;

/// <summary>
/// Resolves a tenant's effective storage limits from the tenant-scope setting overrides,
/// falling back to the code defaults in <see cref="StorageSettingKeys"/>. User-scope
/// overrides are deliberately ignored: a user must not be able to raise their own limits.
/// </summary>
public sealed class StorageLimitsProvider(
    ITenantSettingRepository<StorageDbContext> tenantSettings) : IStorageLimitsProvider
{
    private static readonly StorageSettingKeys _registry = new();

    public async Task<StorageLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TenantSettingEntity> overrides = await tenantSettings.GetAllAsync(
            TenantId.Create(tenantId),
            _registry.ModuleName,
            cancellationToken);

        return StorageLimits.Create(
            ReadInt(overrides, StorageSettingKeys.MaxUploadSizeMb),
            ReadString(overrides, StorageSettingKeys.AllowedFileTypes),
            ReadInt(overrides, StorageSettingKeys.StorageQuotaMb));
    }

    private static int ReadInt(IReadOnlyList<TenantSettingEntity> overrides, SettingDefinition<int> definition)
    {
        TenantSettingEntity? entity = overrides.FirstOrDefault(e => e.SettingKey == definition.Key);
        return entity is not null
            && int.TryParse(entity.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : definition.DefaultValue;
    }

    private static string ReadString(IReadOnlyList<TenantSettingEntity> overrides, SettingDefinition<string> definition)
    {
        TenantSettingEntity? entity = overrides.FirstOrDefault(e => e.SettingKey == definition.Key);
        return entity?.Value ?? definition.DefaultValue;
    }
}
