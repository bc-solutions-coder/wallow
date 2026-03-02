using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Domain.Messaging.Identity;

public readonly record struct MessageId(Guid Value) : IStronglyTypedId<MessageId>
{
    public static MessageId Create(Guid value) => new(value);
    public static MessageId New() => new(Guid.NewGuid());
}
