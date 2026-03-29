using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Domain.Identity;

public readonly record struct ActiveSessionId(Guid Value) : IStronglyTypedId<ActiveSessionId>
{
    public static ActiveSessionId Create(Guid value) => new(value);
    public static ActiveSessionId New() => new(Guid.NewGuid());
}
