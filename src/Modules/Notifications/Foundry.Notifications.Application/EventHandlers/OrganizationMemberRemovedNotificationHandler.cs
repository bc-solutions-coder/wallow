using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Foundry.Notifications.Application.EventHandlers;

public static class OrganizationMemberRemovedNotificationHandler
{
    public static async Task Handle(OrganizationMemberRemovedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "You have been removed from an organization",
            Body: $"Your access to organization {message.OrganizationId} has been revoked. If you believe this was done in error, please contact your organization administrator.");

        await bus.InvokeAsync(emailCommand);
    }
}
