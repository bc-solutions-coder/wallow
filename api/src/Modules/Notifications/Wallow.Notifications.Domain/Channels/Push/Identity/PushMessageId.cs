using Wallow.Shared.Kernel.Identity;

namespace Wallow.Notifications.Domain.Channels.Push.Identity;

public readonly record struct PushMessageId(Guid Value) : IStronglyTypedId<PushMessageId>
{
    public static PushMessageId Create(Guid value) => new(value);
    public static PushMessageId New() => new(Guid.NewGuid());
}
