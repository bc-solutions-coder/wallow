using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class MagicLinkRequestedNotificationHandler
{
    public static async Task Handle(
        MagicLinkRequestedEvent message,
        IMessageBus bus,
        IEmailTemplateService emailTemplateService,
        IConfiguration configuration)
    {
        string authUrl = configuration["ServiceUrls:AuthUrl"]
                        ?? configuration["AuthUrl"]
                        ?? "http://localhost:5002";

        string verifyUrl = $"{authUrl.TrimEnd('/')}/login?magicLinkToken={Uri.EscapeDataString(message.Token)}";

        if (!string.IsNullOrEmpty(message.ReturnUrl))
        {
            verifyUrl += $"&returnUrl={Uri.EscapeDataString(message.ReturnUrl)}";
        }

        if (!string.IsNullOrEmpty(message.ClientId))
        {
            verifyUrl += $"&client_id={Uri.EscapeDataString(message.ClientId)}";
        }

        string body = await emailTemplateService.RenderAsync("magiclink", new
        {
            message.Email,
            VerifyUrl = verifyUrl
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Your Magic Link",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
