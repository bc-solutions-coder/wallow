using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct InitialAccessTokenId(Guid Value) : IStronglyTypedId<InitialAccessTokenId>
{
    public static InitialAccessTokenId Create(Guid value) => new(value);
    public static InitialAccessTokenId New() => new(Guid.NewGuid());
}
