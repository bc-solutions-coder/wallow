using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Contracts.Communications.Announcements.Events;
using Foundry.Shared.Kernel.Results;
using Wolverine;

namespace Foundry.Communications.Application.Announcements.Commands.PublishAnnouncement;

public sealed record PublishAnnouncementCommand(Guid Id);

public sealed class PublishAnnouncementHandler(
    IAnnouncementRepository repository,
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

        // Publish integration event for cross-module communication
        await bus.PublishAsync(new AnnouncementPublishedEvent
        {
            AnnouncementId = announcement.Id.Value,
            TenantId = Guid.Empty,
            Title = announcement.Title,
            Content = announcement.Content,
            Type = announcement.Type.ToString(),
            Target = announcement.Target.ToString(),
            TargetValue = announcement.TargetValue,
            IsPinned = announcement.IsPinned
        });

        return Result.Success();
    }
}
