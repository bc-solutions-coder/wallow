using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Contracts.Announcements.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

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
