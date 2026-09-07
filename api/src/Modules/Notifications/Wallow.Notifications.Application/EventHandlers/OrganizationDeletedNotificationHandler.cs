using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationDeletedNotificationHandler
{
    /// <summary>
    /// Uses admin addresses captured before deletion because memberships no longer exist.
    /// An empty recipient list is valid. Links to the organizations list, not the deleted page.
    /// </summary>
    public static async Task Handle(
        OrganizationDeletedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus,
        IConfiguration configuration)
    {
        if (message.RecipientEmails.Count == 0)
        {
            return;
        }

        string body = await templateService.RenderAsync("organizationdeleted", new
        {
            message.OrganizationName,
            DashboardUrl = PlatformSuspensionEmails.DashboardUrl(configuration)
        });

        await PlatformSuspensionEmails.SendAsync(
            bus,
            message.RecipientEmails,
            $"{message.OrganizationName} has been deleted",
            body);
    }
}
