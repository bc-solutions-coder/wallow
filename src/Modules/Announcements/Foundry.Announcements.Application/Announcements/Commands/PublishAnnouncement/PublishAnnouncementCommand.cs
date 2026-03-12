using Foundry.Announcements.Application.Announcements.Interfaces;
using Foundry.Announcements.Application.Announcements.Services;
using Foundry.Announcements.Domain.Announcements.Entities;
using Foundry.Announcements.Domain.Announcements.Identity;
using Foundry.Shared.Contracts.Announcements.Events;
using Foundry.Shared.Kernel.Results;
using Wolverine;

namespace Foundry.Announcements.Application.Announcements.Commands.PublishAnnouncement;

public sealed record PublishAnnouncementCommand(Guid Id);

public sealed class PublishAnnouncementHandler(
    IAnnouncementRepository repository,
    IAnnouncementTargetingService targetingService,
    IMessageBus bus,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(PublishAnnouncementCommand command, CancellationToken ct)
    {
        Announcement? announcement = await repository.GetByIdAsync(AnnouncementId.Create(command.Id), ct);
        if (announcement is null)
        {
            return Result.Failure(Error.NotFound("Announcement.NotFound", "Announcement not found"));
        }

        announcement.Publish(timeProvider);
        await repository.UpdateAsync(announcement, ct);

        IReadOnlyList<Guid> targetUserIds = await targetingService.ResolveTargetUsersAsync(announcement, ct);

        await bus.PublishAsync(new AnnouncementPublishedEvent
        {
            AnnouncementId = announcement.Id.Value,
            TenantId = announcement.TenantId.Value,
            Title = announcement.Title,
            Content = announcement.Content,
            Type = announcement.Type.ToString(),
            Target = announcement.Target.ToString(),
            TargetValue = announcement.TargetValue,
            IsPinned = announcement.IsPinned,
            TargetUserIds = targetUserIds
        });

        return Result.Success();
    }
}
