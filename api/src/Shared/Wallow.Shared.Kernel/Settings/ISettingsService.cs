namespace Wallow.Shared.Kernel.Settings;

public interface ISettingsService
{
    // Read — merged: user > tenant > code default
    Task<IReadOnlyList<ResolvedSetting>> GetUserSettingsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedSetting>> GetTenantSettingsAsync(Guid tenantId, CancellationToken ct = default);
    Task<ResolvedSettingsConfig> GetConfigAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    // Write
    Task UpdateTenantSettingsAsync(Guid tenantId, IReadOnlyList<SettingUpdate> settings, Guid updatedBy, CancellationToken ct = default);
    Task UpdateUserSettingsAsync(Guid tenantId, Guid userId, IReadOnlyList<SettingUpdate> settings, CancellationToken ct = default);

    // Delete
    Task DeleteTenantSettingsAsync(Guid tenantId, IReadOnlyList<string> keys, Guid deletedBy, CancellationToken ct = default);
    Task DeleteUserSettingsAsync(Guid tenantId, Guid userId, IReadOnlyList<string> keys, CancellationToken ct = default);
}

public sealed record ResolvedSetting(
    string Key,
    string Value,
    string Source,           // "user", "tenant", "default"
    string? DisplayName,     // null for custom/system keys
    string? Description,     // null for custom/system keys
    string? DefaultValue);   // null for custom/system keys

public sealed record ResolvedSettingsConfig(
    IReadOnlyDictionary<string, string> Settings);

public sealed record SettingUpdate(string Key, string Value);
