using Microsoft.Extensions.Logging;
using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryCommentAddedInAppHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task Handle_SendsInAppNotification_WhenNotInternal()
    {
        Guid submitterUserId = Guid.NewGuid();

        InquiryCommentAddedEvent @event = new()
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Agent Smith",
            IsInternal = false,
            SubmitterUserId = submitterUserId,
            SubmitterEmail = "submitter@test.com",
            SubmitterName = "Test Submitter",
            InquirySubject = "Help needed",
            CommentContent = "We are looking into this."
        };

        await InquiryCommentAddedInAppHandler.Handle(@event, _bus, _logger);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd =>
                cmd.UserId == submitterUserId &&
                cmd.Type == NotificationType.InquiryComment &&
                cmd.ActionUrl == $"/dashboard/inquiries/{@event.InquiryId}" &&
                cmd.SourceModule == "Inquiries"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_SkipsNotification_WhenIsInternal()
    {
        InquiryCommentAddedEvent @event = new()
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Agent Smith",
            IsInternal = true,
            SubmitterUserId = Guid.NewGuid(),
            InquirySubject = "Help needed",
            CommentContent = "Internal note."
        };

        await InquiryCommentAddedInAppHandler.Handle(@event, _bus, _logger);

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendNotificationCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_SkipsNotification_WhenSubmitterUserIdIsNull()
    {
        InquiryCommentAddedEvent @event = new()
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Agent Smith",
            IsInternal = false,
            SubmitterUserId = null,
            InquirySubject = "Help needed",
            CommentContent = "We are looking into this."
        };

        await InquiryCommentAddedInAppHandler.Handle(@event, _bus, _logger);

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendNotificationCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
