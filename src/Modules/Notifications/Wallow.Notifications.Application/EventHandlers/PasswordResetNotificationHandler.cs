using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class PasswordResetNotificationHandler
{
    public static async Task Handle(PasswordResetRequestedEvent message, IMessageBus bus,
        IEmailTemplateService templateService)
    {
        string body = await templateService.RenderAsync("passwordreset", new
        {
            message.ResetUrl,
            message.Email
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Password Reset Request",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
