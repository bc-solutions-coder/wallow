using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationPlatformSuspendedNotificationHandler
{
    /// <summary>
    /// One email per recipient, carrying the operator's reason. An event with no recipients
    /// sends nothing and is not an error: the suspension itself is the durable record, and the
    /// reason stays readable on the organization. Lifting a suspension sends no email — this
    /// notice is the only one.
    /// </summary>
    public static async Task Handle(
        OrganizationSuspendedByPlatformEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus,
        IConfiguration configuration)
    {
        if (message.RecipientEmails.Count == 0)
        {
            return;
        }

        string body = await templateService.RenderAsync("organizationplatformsuspended", new
        {
            message.OrganizationName,
            message.Reason,
            OrganizationUrl = PlatformSuspensionEmails.OrganizationUrl(configuration, message.TenantId)
        });

        await PlatformSuspensionEmails.SendAsync(
            bus,
            message.RecipientEmails,
            $"{message.OrganizationName} has been suspended by the platform",
            body);
    }
}
