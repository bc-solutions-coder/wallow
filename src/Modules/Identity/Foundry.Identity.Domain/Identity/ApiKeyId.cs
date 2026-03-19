using Foundry.Shared.Kernel.Identity;

namespace Foundry.Identity.Domain.Identity;

public readonly record struct ApiKeyId(Guid Value) : IStronglyTypedId<ApiKeyId>
{
    public static ApiKeyId Create(Guid value) => new(value);
    public static ApiKeyId New() => new(Guid.NewGuid());
}
