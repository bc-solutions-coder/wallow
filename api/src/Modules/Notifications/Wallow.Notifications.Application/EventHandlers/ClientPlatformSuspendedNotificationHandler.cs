using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class ClientPlatformSuspendedNotificationHandler
{
    /// <summary>
    /// Invokes one email command per recipient with the operator's suspension reason.
    /// An empty recipient list is valid. Reinstatement has no email handler.
    /// </summary>
    public static async Task Handle(
        ClientSuspendedByPlatformEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus,
        IConfiguration configuration)
    {
        if (message.RecipientEmails.Count == 0)
        {
            return;
        }

        string body = await templateService.RenderAsync("clientplatformsuspended", new
        {
            message.ClientName,
            message.OrganizationName,
            message.Reason,
            OrganizationUrl = PlatformSuspensionEmails.OrganizationUrl(configuration, message.OrganizationId)
        });

        await PlatformSuspensionEmails.SendAsync(
            bus,
            message.RecipientEmails,
            $"{message.ClientName} has been suspended by the platform",
            body);
    }
}
