using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationCreatedNotificationHandler
{
    public static async Task Handle(OrganizationCreatedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.CreatorEmail,
            From: null,
            Subject: $"Welcome to {message.Name}",
            Body: $"Your organization '{message.Name}' has been successfully created. You can now start configuring your workspace and inviting team members.");

        await bus.InvokeAsync(emailCommand);
    }
}
