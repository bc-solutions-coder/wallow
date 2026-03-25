using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryCommentAddedNotificationHandler
{
    public static async Task Handle(InquiryCommentAddedEvent message, IMessageBus bus)
    {
        if (message.IsInternal)
        {
            return;
        }

        string body = $"""
            <h2>New Comment on Your Inquiry</h2>
            <p><strong>Inquiry:</strong> {message.InquirySubject}</p>
            <p><strong>From:</strong> {message.AuthorName}</p>
            <p>{message.CommentContent}</p>
            """;

        SendEmailCommand emailCommand = new(
            To: message.SubmitterEmail,
            From: null,
            Subject: "New Comment on Your Inquiry",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
