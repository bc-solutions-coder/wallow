using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static partial class InquiryCommentAddedInAppHandler
{
    public static async Task Handle(InquiryCommentAddedEvent message, IMessageBus bus, ILogger logger)
    {
        if (message.IsInternal)
        {
            LogSkippedInternal(logger, message.InquiryId, message.InquiryCommentId);
            return;
        }

        if (message.SubmitterUserId == null)
        {
            LogSkippedNoUser(logger, message.InquiryId, message.InquiryCommentId);
            return;
        }

        SendNotificationCommand command = new(
            UserId: message.SubmitterUserId.Value,
            Type: NotificationType.InquiryComment,
            Title: $"New comment on your inquiry: {message.InquirySubject}",
            Message: message.CommentContent,
            ActionUrl: $"/dashboard/inquiries/{message.InquiryId}",
            SourceModule: "Inquiries");

        LogSendingNotification(logger, message.InquiryId, message.SubmitterUserId.Value, command.ActionUrl!);

        await bus.InvokeAsync(command);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping inquiry comment notification: comment {CommentId} on inquiry {InquiryId} is internal")]
    private static partial void LogSkippedInternal(ILogger logger, Guid inquiryId, Guid commentId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping inquiry comment notification: comment {CommentId} on inquiry {InquiryId} has no submitter user ID")]
    private static partial void LogSkippedNoUser(ILogger logger, Guid inquiryId, Guid commentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sending inquiry comment in-app notification for inquiry {InquiryId} to user {UserId}, actionUrl={ActionUrl}")]
    private static partial void LogSendingNotification(ILogger logger, Guid inquiryId, Guid userId, string actionUrl);
}
