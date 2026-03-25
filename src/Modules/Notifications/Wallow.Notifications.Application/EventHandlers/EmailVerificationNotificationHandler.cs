using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class EmailVerificationNotificationHandler
{
    public static async Task Handle(
        EmailVerificationRequestedEvent message,
        IEmailTemplateService templateService,
        IMessageBus bus)
    {
        string body = await templateService.RenderAsync("emailverification", new
        {
            message.FirstName,
            message.VerifyUrl
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Verify your email address",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
