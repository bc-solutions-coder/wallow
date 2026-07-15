using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Infrastructure.Settings;

public readonly record struct UserSettingId(Guid Value) : IStronglyTypedId<UserSettingId>
{
    public static UserSettingId Create(Guid value) => new(value);
    public static UserSettingId New() => new(Guid.NewGuid());
}

public sealed class UserSettingEntity : AuditableEntity<UserSettingId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string UserId { get; private set; } = string.Empty;
    public string ModuleKey { get; private set; } = string.Empty;
    public string SettingKey { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;

    // ReSharper disable once UnusedMember.Local
    private UserSettingEntity() { } // EF Core

    public UserSettingEntity(TenantId tenantId, string userId, string moduleKey, string settingKey, string value)
    {
        Id = UserSettingId.New();
        TenantId = tenantId;
        UserId = userId;
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
