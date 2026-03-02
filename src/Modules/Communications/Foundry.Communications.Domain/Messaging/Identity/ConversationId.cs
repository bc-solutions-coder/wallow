using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Domain.Messaging.Identity;

public readonly record struct ConversationId(Guid Value) : IStronglyTypedId<ConversationId>
{
    public static ConversationId Create(Guid value) => new(value);
    public static ConversationId New() => new(Guid.NewGuid());
}
