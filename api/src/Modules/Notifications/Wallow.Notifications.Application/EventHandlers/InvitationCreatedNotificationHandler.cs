using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class InvitationCreatedNotificationHandler
{
    public static async Task Handle(
        InvitationCreatedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus,
        IConfiguration configuration)
    {
        string authUrl = configuration["ServiceUrls:AuthUrl"]
                        ?? configuration["AuthUrl"]
                        ?? "http://localhost:5002";

        string invitationUrl = $"{authUrl.TrimEnd('/')}/invitation?token={Uri.EscapeDataString(message.Token)}";

        string body = await templateService.RenderAsync("invitation", new
        {
            message.Email,
            InvitationUrl = invitationUrl,
            message.ExpiresAt
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "You've Been Invited to Join an Organization",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
