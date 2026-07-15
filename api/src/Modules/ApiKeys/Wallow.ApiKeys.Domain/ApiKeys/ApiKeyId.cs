using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Domain.ApiKeys;

public readonly record struct ApiKeyId(Guid Value) : IStronglyTypedId<ApiKeyId>
{
    public static ApiKeyId Create(Guid value) => new(value);
    public static ApiKeyId New() => new(Guid.NewGuid());
}
