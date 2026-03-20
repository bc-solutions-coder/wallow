using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct ApiScopeId(Guid Value) : IStronglyTypedId<ApiScopeId>
{
    public static ApiScopeId Create(Guid value) => new(value);
    public static ApiScopeId New() => new(Guid.NewGuid());
}
