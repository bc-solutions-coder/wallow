using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct OrganizationDomainId(Guid Value) : IStronglyTypedId<OrganizationDomainId>
{
    public static OrganizationDomainId Create(Guid value) => new(value);
    public static OrganizationDomainId New() => new(Guid.NewGuid());
}
