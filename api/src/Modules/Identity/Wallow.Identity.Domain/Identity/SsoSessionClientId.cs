using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct SsoSessionClientId(Guid Value) : IStronglyTypedId<SsoSessionClientId>
{
    public static SsoSessionClientId Create(Guid value) => new(value);
    public static SsoSessionClientId New() => new(Guid.NewGuid());
}
