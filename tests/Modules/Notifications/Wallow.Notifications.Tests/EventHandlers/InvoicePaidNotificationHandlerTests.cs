using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Billing.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InvoicePaidNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsPaymentReceiptEmailToUser()
    {
        InvoicePaidEvent @event = new()
        {
            InvoiceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "payer@test.com",
            InvoiceNumber = "INV-042",
            Amount = 99.99m,
            Currency = "USD",
            PaidAt = DateTime.UtcNow
        };

        await InvoicePaidNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "payer@test.com" &&
                cmd.Subject.Contains("INV-042") &&
                cmd.Body.Contains("INV-042")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
