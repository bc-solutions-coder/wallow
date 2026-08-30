using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct RegisteredClientId(Guid Value) : IStronglyTypedId<RegisteredClientId>
{
    public static RegisteredClientId Create(Guid value) => new(value);
    public static RegisteredClientId New() => new(Guid.NewGuid());
}
