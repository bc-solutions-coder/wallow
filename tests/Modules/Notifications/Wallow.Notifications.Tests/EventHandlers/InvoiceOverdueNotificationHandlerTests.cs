using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Billing.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InvoiceOverdueNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsOverdueEmailToUser()
    {
        InvoiceOverdueEvent @event = new()
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "billing@test.com",
            InvoiceNumber = "INV-001",
            Amount = 150.00m,
            Currency = "USD",
            DueDate = DateTime.UtcNow.AddDays(-5)
        };

        await InvoiceOverdueNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "billing@test.com" &&
                cmd.Subject.Contains("INV-001") &&
                cmd.Body.Contains("INV-001")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
