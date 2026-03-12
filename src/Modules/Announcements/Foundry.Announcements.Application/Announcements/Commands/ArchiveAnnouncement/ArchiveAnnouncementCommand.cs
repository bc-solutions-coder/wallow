using Foundry.Announcements.Application.Announcements.Interfaces;
using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Application.Announcements.Commands.ArchiveAnnouncement;

public sealed record ArchiveAnnouncementCommand(Guid Id);

public sealed class ArchiveAnnouncementHandler(
    IAnnouncementRepository repository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(ArchiveAnnouncementCommand command, CancellationToken ct)
    {
        Announcement? announcement = await repository.GetByIdAsync(AnnouncementId.Create(command.Id), ct);
        if (announcement is null)
        {
            return Result.Failure(Error.NotFound("Announcement.NotFound", "Announcement not found"));
        }

        announcement.Archive(timeProvider);
        await repository.UpdateAsync(announcement, ct);

        return Result.Success();
    }
}
