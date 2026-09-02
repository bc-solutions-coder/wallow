using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Announcements.Domain.Errors;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Announcements.Commands.DismissAnnouncement;

public sealed record DismissAnnouncementCommand(Guid AnnouncementId, Guid UserId);

public sealed class DismissAnnouncementHandler(
    IAnnouncementRepository announcementRepository,
    IAnnouncementDismissalRepository dismissalRepository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(DismissAnnouncementCommand command, CancellationToken ct)
    {
        AnnouncementId announcementId = AnnouncementId.Create(command.AnnouncementId);
        UserId userId = UserId.Create(command.UserId);

        Announcement? announcement = await announcementRepository.GetByIdAsync(announcementId, ct);

        if (announcement is null)
        {
            return Result.Failure(AnnouncementsErrors.AnnouncementNotFound);
        }

        if (!announcement.IsDismissible)
        {
            return Result.Failure(AnnouncementsErrors.AnnouncementNotDismissible);
        }

        bool alreadyDismissed = await dismissalRepository.ExistsAsync(announcementId, userId, ct);
        if (alreadyDismissed)
        {
            return Result.Success();
        }

        AnnouncementDismissal dismissal = AnnouncementDismissal.Create(announcementId, userId, timeProvider);
        await dismissalRepository.AddAsync(dismissal, ct);

        return Result.Success();
    }
}
