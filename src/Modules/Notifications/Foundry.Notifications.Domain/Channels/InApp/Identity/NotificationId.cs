using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Domain.Channels.InApp.Identity;

public readonly record struct NotificationId(Guid Value) : IStronglyTypedId<NotificationId>
{
    public static NotificationId Create(Guid value) => new(value);
    public static NotificationId New() => new(Guid.NewGuid());
}
