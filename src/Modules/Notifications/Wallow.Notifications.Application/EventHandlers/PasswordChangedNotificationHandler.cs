using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

public static class PasswordChangedNotificationHandler
{
    public static async Task Handle(PasswordChangedEvent message, IMessageBus bus,
        IEmailTemplateService templateService)
    {
        string body = await templateService.RenderAsync("passwordchanged", new
        {
            message.Email,
            message.FirstName
        });

        SendEmailCommand emailCommand = new(
            To: message.Email,
            From: null,
            Subject: "Your Password Has Been Changed",
            Body: body);

        await bus.InvokeAsync(emailCommand);
    }
}
