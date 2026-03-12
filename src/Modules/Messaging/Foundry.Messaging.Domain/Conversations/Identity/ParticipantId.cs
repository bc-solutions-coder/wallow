using Foundry.Shared.Kernel.Identity;

namespace Foundry.Messaging.Domain.Conversations.Identity;

public readonly record struct ParticipantId(Guid Value) : IStronglyTypedId<ParticipantId>
{
    public static ParticipantId Create(Guid value) => new(value);
    public static ParticipantId New() => new(Guid.NewGuid());
}
