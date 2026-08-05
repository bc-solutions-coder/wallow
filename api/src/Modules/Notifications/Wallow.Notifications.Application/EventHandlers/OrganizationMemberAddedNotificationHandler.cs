using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Configuration;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OrganizationMemberAddedNotificationHandler
{
    public static async Task Handle(
        OrganizationMemberAddedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus,
        IConfiguration configuration)
    {
        string appUrl = configuration["AppUrl"]
                        ?? configuration["AuthUrl"]
                        ?? new ServiceUrlsOptions().AuthUrl;

        string body = await templateService.RenderAsync("organizationmemberadded", new
        {
            AppUrl = appUrl
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "You've Been Added to an Organization",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
