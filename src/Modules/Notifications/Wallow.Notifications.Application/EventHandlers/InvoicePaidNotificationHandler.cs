using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Billing.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InvoicePaidNotificationHandler
{
    public static async Task Handle(InvoicePaidEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.UserEmail,
            From: null,
            Subject: $"Payment Receipt for Invoice {message.InvoiceNumber}",
            Body: $"Thank you for your payment of {message.Amount:C} {message.Currency} for invoice {message.InvoiceNumber}.");

        await bus.InvokeAsync(emailCommand);
    }
}
