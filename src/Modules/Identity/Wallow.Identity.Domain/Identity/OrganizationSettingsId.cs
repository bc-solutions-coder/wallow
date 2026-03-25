using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct OrganizationSettingsId(Guid Value) : IStronglyTypedId<OrganizationSettingsId>
{
    public static OrganizationSettingsId Create(Guid value) => new(value);
    public static OrganizationSettingsId New() => new(Guid.NewGuid());
}
