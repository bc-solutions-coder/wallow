using Foundry.Shared.Kernel.Identity;

namespace Foundry.Messaging.Domain.Conversations.Identity;

public readonly record struct MessageId(Guid Value) : IStronglyTypedId<MessageId>
{
    public static MessageId Create(Guid value) => new(value);
    public static MessageId New() => new(Guid.NewGuid());
}
