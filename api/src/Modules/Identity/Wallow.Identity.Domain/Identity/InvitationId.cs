using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct InvitationId(Guid Value) : IStronglyTypedId<InvitationId>
{
    public static InvitationId Create(Guid value) => new(value);
    public static InvitationId New() => new(Guid.NewGuid());
}
