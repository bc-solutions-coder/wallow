using Foundry.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Contracts.Announcements.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class AnnouncementPublishedNotificationHandler
{
    public static async Task Handle(AnnouncementPublishedEvent message, IMessageBus bus)
    {
        foreach (Guid userId in message.TargetUserIds)
        {
            SendNotificationCommand command = new(
                UserId: userId,
                Type: NotificationType.Announcement,
                Title: message.Title,
                Message: message.Content);

            await bus.InvokeAsync(command);
        }
    }
}
