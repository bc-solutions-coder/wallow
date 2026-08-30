using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class ClientPlatformSuspendedNotificationHandler
{
    /// <summary>
    /// One email per recipient, naming the client and carrying the operator's reason. An event
    /// with no recipients sends nothing and is not an error: the suspension itself is the durable
    /// record, and the reason stays readable on the client. Lifting a suspension sends no email —
    /// this notice is the only one.
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
