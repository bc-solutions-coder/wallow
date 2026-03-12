using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Domain.Channels.Push.Identity;

public readonly record struct DeviceRegistrationId(Guid Value) : IStronglyTypedId<DeviceRegistrationId>
{
    public static DeviceRegistrationId Create(Guid value) => new(value);
    public static DeviceRegistrationId New() => new(Guid.NewGuid());
}
