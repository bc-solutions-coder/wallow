using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InquiryCommentAddedNotificationHandler
{
    public static async Task Handle(
        InquiryCommentAddedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus)
    {
        if (message.IsInternal)
        {
            return;
        }

        string body = await templateService.RenderAsync("inquirycomment", new
        {
            message.SubmitterName,
            message.AuthorName,
            message.InquirySubject,
            message.CommentContent
        });

        SendEmailCommand emailCommand = new(
            To: message.SubmitterEmail,
            From: null,
            Subject: "New Comment on Your Inquiry",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
