using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct MembershipId(Guid Value) : IStronglyTypedId<MembershipId>
{
    public static MembershipId Create(Guid value) => new(value);
    public static MembershipId New() => new(Guid.NewGuid());
}
