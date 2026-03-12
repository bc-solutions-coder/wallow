using Foundry.Shared.Kernel.Identity;

namespace Foundry.Announcements.Domain.Announcements.Identity;

public readonly record struct AnnouncementId(Guid Value) : IStronglyTypedId<AnnouncementId>
{
    public static AnnouncementId Create(Guid value) => new(value);
    public static AnnouncementId New() => new(Guid.NewGuid());
}
