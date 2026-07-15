using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct MembershipRequestId(Guid Value) : IStronglyTypedId<MembershipRequestId>
{
    public static MembershipRequestId Create(Guid value) => new(value);
    public static MembershipRequestId New() => new(Guid.NewGuid());
}
