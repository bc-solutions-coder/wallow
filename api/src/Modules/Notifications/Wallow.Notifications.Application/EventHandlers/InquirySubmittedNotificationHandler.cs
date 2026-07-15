using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquirySubmittedNotificationHandler
{
    public static async Task Handle(InquirySubmittedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.AdminEmail,
            From: null,
            Subject: $"New Inquiry: {message.ProjectType}",
            Body: $"New inquiry from {message.Name} ({message.Email}): {message.Message}");

        await bus.InvokeAsync(emailCommand);
    }
}
