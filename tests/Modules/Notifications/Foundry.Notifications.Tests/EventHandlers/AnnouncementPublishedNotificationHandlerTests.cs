using Foundry.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Foundry.Notifications.Application.EventHandlers;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Contracts.Announcements.Events;
using Wolverine;

namespace Foundry.Notifications.Tests.EventHandlers;

public class AnnouncementPublishedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_FansOutNotificationToAllTargetUsers()
    {
        Guid userId1 = Guid.NewGuid();
        Guid userId2 = Guid.NewGuid();
        Guid userId3 = Guid.NewGuid();

        AnnouncementPublishedEvent @event = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Title = "System Update",
            Content = "We are updating the platform.",
            Type = "General",
            Target = "AllUsers",
            TargetValue = null,
            IsPinned = false,
            TargetUserIds = [userId1, userId2, userId3]
        };

        await AnnouncementPublishedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(3).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd =>
                cmd.Type == NotificationType.Announcement &&
                cmd.Title == "System Update"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == userId1),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == userId2),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == userId3),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
