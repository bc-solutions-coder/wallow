using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryCommentAddedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsEmailToSubmitter_WhenNotInternal()
    {
        InquiryCommentAddedEvent @event = new()
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Support Agent",
            IsInternal = false,
            SubmitterEmail = "submitter@test.com",
            SubmitterName = "Jane Doe",
            InquirySubject = "Billing Question",
            CommentContent = "We have resolved your issue."
        };

        await InquiryCommentAddedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "submitter@test.com" &&
                cmd.Subject == "New Comment on Your Inquiry"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_SkipsEmail_WhenIsInternal()
    {
        InquiryCommentAddedEvent @event = new()
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Internal Agent",
            IsInternal = true,
            SubmitterEmail = "submitter@test.com",
            SubmitterName = "Jane Doe",
            InquirySubject = "Internal Note",
            CommentContent = "This is an internal note."
        };

        await InquiryCommentAddedNotificationHandler.Handle(@event, _bus);

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendEmailCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_SendsCorrectEmailBody()
    {
        InquiryCommentAddedEvent @event = new()
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Alice Smith",
            IsInternal = false,
            SubmitterEmail = "bob@test.com",
            SubmitterName = "Bob",
            InquirySubject = "Feature Request",
            CommentContent = "Thank you for your feedback."
        };

        await InquiryCommentAddedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.Body.Contains("Alice Smith") &&
                cmd.Body.Contains("Thank you for your feedback.")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
