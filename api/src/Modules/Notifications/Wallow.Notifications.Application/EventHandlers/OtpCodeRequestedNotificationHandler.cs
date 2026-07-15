using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class OtpCodeRequestedNotificationHandler
{
    public static async Task Handle(
        OtpCodeRequestedEvent message,
        IMessageBus bus,
        IEmailTemplateService emailTemplateService)
    {
        string body = await emailTemplateService.RenderAsync("otpcode", new
        {
            message.Email,
            message.Code
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Your Login Code",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
