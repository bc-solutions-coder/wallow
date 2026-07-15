using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryStatusChangedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsStatusChangeEmailToSubmitter()
    {
        Guid inquiryId = Guid.NewGuid();

        InquiryStatusChangedEvent @event = new()
        {
            InquiryId = inquiryId,
            OldStatus = "Open",
            NewStatus = "InProgress",
            ChangedAt = DateTime.UtcNow,
            SubmitterEmail = "submitter@test.com"
        };

        await InquiryStatusChangedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "submitter@test.com" &&
                cmd.Subject.Contains(inquiryId.ToString()) &&
                cmd.Body.Contains("Open") &&
                cmd.Body.Contains("InProgress")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
