using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationDeletedNotificationHandler
{
    /// <summary>
    /// One email per admin the organization had at the moment it died — the event carries their
    /// addresses because the memberships no longer exist to resolve them from. An event with no
    /// recipients sends nothing and is not an error. The notice points at the organizations
    /// list, since the deleted organization's own page is gone.
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
