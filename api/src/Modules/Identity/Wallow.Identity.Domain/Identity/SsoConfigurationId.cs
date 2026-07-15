using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct SsoConfigurationId(Guid Value) : IStronglyTypedId<SsoConfigurationId>
{
    public static SsoConfigurationId Create(Guid value) => new(value);
    public static SsoConfigurationId New() => new(Guid.NewGuid());
}
