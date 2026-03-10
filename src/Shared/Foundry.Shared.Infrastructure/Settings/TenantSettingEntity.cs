using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Shared.Infrastructure.Settings;

public readonly record struct TenantSettingId(Guid Value) : IStronglyTypedId<TenantSettingId>
{
    public static TenantSettingId Create(Guid value) => new(value);
    public static TenantSettingId New() => new(Guid.NewGuid());
}

public sealed class TenantSettingEntity : AuditableEntity<TenantSettingId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string ModuleKey { get; private set; } = string.Empty;
    public string SettingKey { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    // ReSharper disable once UnusedMember.Local
    private TenantSettingEntity() { } // EF Core

    public TenantSettingEntity(TenantId tenantId, string moduleKey, string settingKey, string value)
    {
        Id = TenantSettingId.New();
        TenantId = tenantId;
        ModuleKey = moduleKey;
        SettingKey = settingKey;
        Value = value;
        SetCreated(DateTimeOffset.UtcNow);
    }

    public void UpdateValue(string value)
    {
        Value = value;
        SetUpdated(DateTimeOffset.UtcNow);
    }
}
