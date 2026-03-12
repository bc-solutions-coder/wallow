using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class InquirySubmittedNotificationHandler
{
    public static async Task Handle(InquirySubmittedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.AdminEmail,
            From: null,
            Subject: $"New Inquiry: {message.Subject}",
            Body: $"New inquiry from {message.Name} ({message.Email}): {message.Message}");

        await bus.InvokeAsync(emailCommand);
    }
}
