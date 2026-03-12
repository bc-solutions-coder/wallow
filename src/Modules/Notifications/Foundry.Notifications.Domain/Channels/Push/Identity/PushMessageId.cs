using Foundry.Shared.Kernel.Identity;

namespace Foundry.Notifications.Domain.Channels.Push.Identity;

public readonly record struct PushMessageId(Guid Value) : IStronglyTypedId<PushMessageId>
{
    public static PushMessageId Create(Guid value) => new(value);
    public static PushMessageId New() => new(Guid.NewGuid());
}
