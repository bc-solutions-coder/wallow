using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Billing.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class PaymentReceivedNotificationHandler
{
    public static async Task Handle(PaymentReceivedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.UserEmail,
            From: null,
            Subject: "Payment Received",
            Body: $"Your payment of {message.Amount:C} {message.Currency} via {message.PaymentMethod} has been received. Thank you!");

        await bus.InvokeAsync(emailCommand);
    }
}
