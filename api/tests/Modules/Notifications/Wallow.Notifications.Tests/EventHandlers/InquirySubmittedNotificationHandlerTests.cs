using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquirySubmittedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsEmailToAdmin()
    {
        InquirySubmittedEvent @event = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "Jane Doe",
            Email = "jane@test.com",
            Phone = "555-0100",
            ProjectType = "Sales Question",
            Message = "I have a question about pricing.",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@company.com",
            AdminUserIds = []
        };

        await InquirySubmittedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "admin@company.com" &&
                cmd.Subject.Contains("Sales Question") &&
                cmd.Body.Contains("Jane Doe")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
