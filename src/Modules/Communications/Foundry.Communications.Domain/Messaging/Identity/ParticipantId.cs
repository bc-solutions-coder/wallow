using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Domain.Messaging.Identity;

public readonly record struct ParticipantId(Guid Value) : IStronglyTypedId<ParticipantId>
{
    public static ParticipantId Create(Guid value) => new(value);
    public static ParticipantId New() => new(Guid.NewGuid());
}
