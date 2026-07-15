using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquirySubmittedInAppHandler
{
    public static async Task Handle(InquirySubmittedEvent message, IMessageBus bus)
    {
        if (message.AdminUserIds.Count == 0)
        {
            return;
        }

        foreach (Guid adminId in message.AdminUserIds)
        {
            SendNotificationCommand command = new(
                UserId: adminId,
                Type: NotificationType.InquirySubmitted,
                Title: $"New Inquiry: {message.ProjectType}",
                Message: $"From {message.Name} ({message.Email})");

            await bus.InvokeAsync(command);
        }
    }
}
