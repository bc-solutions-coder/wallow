using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct OrganizationBrandingId(Guid Value) : IStronglyTypedId<OrganizationBrandingId>
{
    public static OrganizationBrandingId Create(Guid value) => new(value);
    public static OrganizationBrandingId New() => new(Guid.NewGuid());
}
