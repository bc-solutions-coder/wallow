using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationMemberAddedNotificationHandler
{
    public static async Task Handle(OrganizationMemberAddedEvent message, IMessageBus bus)
    {
        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "You've Been Added to an Organization",
            Body: "You have been added as a member of an organization. Log in to view your new organization and get started.");

        await bus.InvokeAsync(emailCommand);
    }
}
