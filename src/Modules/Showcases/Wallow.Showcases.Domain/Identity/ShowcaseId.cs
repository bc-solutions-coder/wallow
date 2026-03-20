using Wallow.Shared.Kernel.Identity;

namespace Wallow.Showcases.Domain.Identity;

public readonly record struct ShowcaseId(Guid Value) : IStronglyTypedId<ShowcaseId>
{
    public static ShowcaseId Create(Guid value) => new(value);
    public static ShowcaseId New() => new(Guid.NewGuid());
}
