using Foundry.Announcements.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Announcements.Domain.Announcements.Entities;

public sealed class AnnouncementDismissal : Entity<AnnouncementDismissalId>
{
    public AnnouncementId AnnouncementId { get; private set; }
    public UserId UserId { get; private set; }
    public DateTime DismissedAt { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private AnnouncementDismissal() { } // EF Core

    private AnnouncementDismissal(AnnouncementId announcementId, UserId userId, TimeProvider timeProvider)
        : base(AnnouncementDismissalId.New())
    {
        AnnouncementId = announcementId;
        UserId = userId;
        DismissedAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    public static AnnouncementDismissal Create(AnnouncementId announcementId, UserId userId, TimeProvider timeProvider)
    {
        return new AnnouncementDismissal(announcementId, userId, timeProvider);
    }
}
