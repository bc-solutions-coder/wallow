using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationPlatformSuspendedNotificationHandler
{
    /// <summary>
    /// Invokes one email command per recipient with the operator's suspension reason.
    /// An empty recipient list is valid. Reinstatement has no email handler.
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
