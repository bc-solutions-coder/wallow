using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Sms.Commands.SendSms;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class UserRegisteredNotificationHandler
{
    public static async Task Handle(UserRegisteredEvent message, IMessageBus bus,
        IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(message.PhoneNumber))
        {
            string appName = configuration["Branding:AppName"] ?? "Wallow";

            SendSmsCommand smsCommand = new(
                To: message.PhoneNumber,
                Body: $"Welcome to {appName}, {message.FirstName}! Your account is ready.");

            await bus.InvokeAsync(smsCommand);
        }
    }
}
