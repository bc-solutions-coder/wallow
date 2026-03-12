using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Domain.Channels.Push.Identity;

public readonly record struct TenantPushConfigurationId(Guid Value) : IStronglyTypedId<TenantPushConfigurationId>
{
    public static TenantPushConfigurationId Create(Guid value) => new(value);
    public static TenantPushConfigurationId New() => new(Guid.NewGuid());
}
