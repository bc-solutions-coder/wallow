using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Notifications.Application.EventHandlers;
using Foundry.Shared.Contracts.Billing.Events;
using Wolverine;

namespace Foundry.Notifications.Tests.EventHandlers;

public class PaymentReceivedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsConfirmationEmailToUser()
    {
        PaymentReceivedEvent @event = new()
        {
            PaymentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "payer@test.com",
            Amount = 250.00m,
            Currency = "EUR",
            PaymentMethod = "Credit Card",
            PaidAt = DateTime.UtcNow
        };

        await PaymentReceivedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "payer@test.com" &&
                cmd.Subject.Contains("Payment Received") &&
                cmd.Body.Contains("Credit Card")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
