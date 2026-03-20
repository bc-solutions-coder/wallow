using Wallow.Shared.Kernel.Identity;

namespace Wallow.Announcements.Domain.Changelogs.Identity;

public readonly record struct ChangelogItemId(Guid Value) : IStronglyTypedId<ChangelogItemId>
{
    public static ChangelogItemId Create(Guid value) => new(value);
    public static ChangelogItemId New() => new(Guid.NewGuid());
}
