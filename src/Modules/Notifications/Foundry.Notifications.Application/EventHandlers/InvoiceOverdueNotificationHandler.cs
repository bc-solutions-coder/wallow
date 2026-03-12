using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Shared.Contracts.Billing.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class InvoiceOverdueNotificationHandler
{
    public static async Task Handle(InvoiceOverdueEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.UserEmail,
            From: null,
            Subject: $"Invoice {message.InvoiceNumber} is Overdue",
            Body: $"Your invoice {message.InvoiceNumber} for {message.Amount:C} {message.Currency} was due on {message.DueDate:d}. Please make payment as soon as possible.");

        await bus.InvokeAsync(emailCommand);
    }
}
