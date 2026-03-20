using Wallow.Announcements.Domain.Changelogs.Enums;
using Wallow.Announcements.Domain.Changelogs.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Announcements.Domain.Changelogs.Entities;

public sealed class ChangelogItem : Entity<ChangelogItemId>
{
    public ChangelogEntryId EntryId { get; private set; }
    public string Description { get; private set; } = null!;
    public ChangeType Type { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private ChangelogItem() { } // EF Core

    private ChangelogItem(ChangelogEntryId entryId, string description, ChangeType type)
        : base(ChangelogItemId.New())
    {
        EntryId = entryId;
        Description = description;
        Type = type;
    }

    public static ChangelogItem Create(ChangelogEntryId entryId, string description, ChangeType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        return new ChangelogItem(entryId, description, type);
    }

    public void Update(string description, ChangeType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
        Type = type;
    }
}
