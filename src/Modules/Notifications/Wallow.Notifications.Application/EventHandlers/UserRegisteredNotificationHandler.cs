using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Sms.Commands.SendSms;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class UserRegisteredNotificationHandler
{
    public static async Task Handle(UserRegisteredEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Welcome to Wallow",
            Body: $"Welcome {message.FirstName}! Your account has been created.");

        await bus.InvokeAsync(emailCommand);

        if (!string.IsNullOrWhiteSpace(message.PhoneNumber))
        {
            SendSmsCommand smsCommand = new(
                To: message.PhoneNumber,
                Body: $"Welcome to Wallow, {message.FirstName}! Your account is ready.");

            await bus.InvokeAsync(smsCommand);
        }
    }
}
