using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Domain.Identity;

public readonly record struct ClientBrandingId(Guid Value) : IStronglyTypedId<ClientBrandingId>
{
    public static ClientBrandingId Create(Guid value) => new(value);
    public static ClientBrandingId New() => new(Guid.NewGuid());
}
