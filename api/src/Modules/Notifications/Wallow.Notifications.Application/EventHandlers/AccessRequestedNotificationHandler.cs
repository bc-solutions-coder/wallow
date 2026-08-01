using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Configuration;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class AccessRequestedNotificationHandler
{
    /// <summary>
    /// One email per recipient. An event carrying no recipients sends nothing and is not an
    /// error: the pending membership is the durable record of the request, and Identity has
    /// already decided there is nobody to tell.
    /// </summary>
    public static async Task Handle(
        AccessRequestedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus,
        IConfiguration configuration)
    {
        if (message.RecipientEmails.Count == 0)
        {
            return;
        }

        string webUrl = configuration["ServiceUrls:WebUrl"]
                        ?? configuration["WebUrl"]
                        ?? new ServiceUrlsOptions().WebUrl;

        string reviewUrl = $"{webUrl.TrimEnd('/')}/dashboard/organizations/{message.TenantId}";

        string body = await templateService.RenderAsync("accessrequest", new
        {
            message.OrganizationName,
            message.RequesterName,
            message.RequesterEmail,
            ReviewUrl = reviewUrl
        });

        foreach (string recipient in message.RecipientEmails)
        {
            SendEmailCommand emailCommand = new(
                To: recipient,
                From: null,
                Subject: $"Access request for {message.OrganizationName}",
                Body: body);

            await bus.InvokeAsync(emailCommand);
        }
    }
}
