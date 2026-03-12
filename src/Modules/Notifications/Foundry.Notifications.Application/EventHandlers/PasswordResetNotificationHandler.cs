using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class PasswordResetNotificationHandler
{
    public static async Task Handle(PasswordResetRequestedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Password Reset Request",
            Body: $"Use this token to reset your password: {message.ResetToken}");

        await bus.InvokeAsync(emailCommand);
    }
}
