using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class EmailVerifiedNotificationHandler
{
    public static async Task Handle(
        EmailVerifiedEvent message,
        IMessageBus bus,
        IEmailTemplateService emailTemplateService,
        IConfiguration configuration)
    {
        string appUrl = configuration["AppUrl"]
                        ?? configuration["AuthUrl"]
                        ?? "http://localhost:5000";

        string appName = configuration["Branding:AppName"] ?? "Wallow";

        string body = await emailTemplateService.RenderAsync("welcomeemail", new
        {
            message.FirstName,
            message.LastName,
            message.Email,
            AppUrl = appUrl
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: $"Welcome to {appName}",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
