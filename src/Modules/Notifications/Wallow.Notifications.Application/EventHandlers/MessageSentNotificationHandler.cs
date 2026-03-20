using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Contracts.Messaging.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class MessageSentNotificationHandler
{
    public static async Task Handle(MessageSentIntegrationEvent message, IMessageBus bus)
    {
        foreach (Guid participantId in message.ParticipantIds)
        {
            if (participantId == message.SenderId)
            {
                continue;
            }

            SendNotificationCommand command = new(
                UserId: participantId,
                Type: NotificationType.Mention,
                Title: "New Message",
                Message: message.Content);

            await bus.InvokeAsync(command);
        }
    }
}
