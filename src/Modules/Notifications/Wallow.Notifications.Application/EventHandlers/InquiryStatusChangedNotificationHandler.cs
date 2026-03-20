using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryStatusChangedNotificationHandler
{
    public static async Task Handle(InquiryStatusChangedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.SubmitterEmail,
            From: null,
            Subject: $"Inquiry {message.InquiryId} Status Changed",
            Body: $"Your inquiry {message.InquiryId} status has changed from {message.OldStatus} to {message.NewStatus}.");

        await bus.InvokeAsync(emailCommand);
    }
}
