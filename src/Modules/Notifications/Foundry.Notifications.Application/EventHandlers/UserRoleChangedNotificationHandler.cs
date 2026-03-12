using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class UserRoleChangedNotificationHandler
{
    public static async Task Handle(UserRoleChangedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Your Role Has Been Updated",
            Body: $"Your role has been changed from {message.OldRole} to {message.NewRole}. If you did not expect this change, please contact your administrator.");

        await bus.InvokeAsync(emailCommand);
    }
}
