using Foundry.Shared.Kernel.Identity;

namespace Foundry.Announcements.Domain.Changelogs.Identity;

public readonly record struct ChangelogEntryId(Guid Value) : IStronglyTypedId<ChangelogEntryId>
{
    public static ChangelogEntryId Create(Guid value) => new(value);
    public static ChangelogEntryId New() => new(Guid.NewGuid());
}
