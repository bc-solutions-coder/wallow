using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Notifications.Application.Channels.Sms.Commands.SendSms;
using Foundry.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class UserRegisteredNotificationHandler
{
    public static async Task Handle(UserRegisteredEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Welcome to Foundry",
            Body: $"Welcome {message.FirstName}! Your account has been created.");

        await bus.InvokeAsync(emailCommand);

        if (!string.IsNullOrWhiteSpace(message.PhoneNumber))
        {
            SendSmsCommand smsCommand = new(
                To: message.PhoneNumber,
                Body: $"Welcome to Foundry, {message.FirstName}! Your account is ready.");

            await bus.InvokeAsync(smsCommand);
        }
    }
}
