using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class PasswordChangedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();

    [Fact]
    public async Task Handle_RendersPasswordChangedTemplateWithCorrectModel()
    {
        PasswordChangedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "Alice"
        };

        _templateService.RenderAsync("passwordchanged", Arg.Any<object>())
            .Returns("Your password has been changed.");

        await PasswordChangedNotificationHandler.Handle(@event, _bus, _templateService);

        await _templateService.Received(1).RenderAsync(
            "passwordchanged",
            Arg.Is<object>(model =>
                model.GetType().GetProperty("Email")!.GetValue(model)!.ToString() == "user@test.com" &&
                model.GetType().GetProperty("FirstName")!.GetValue(model)!.ToString() == "Alice"));
    }

    [Fact]
    public async Task Handle_SendsEmailWithRenderedBodyAndCorrectSubject()
    {
        PasswordChangedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "Alice"
        };

        _templateService.RenderAsync("passwordchanged", Arg.Any<object>())
            .Returns("Your password has been changed.");

        await PasswordChangedNotificationHandler.Handle(@event, _bus, _templateService);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "user@test.com" &&
                cmd.Subject == "Your Password Has Been Changed" &&
                cmd.Body == "Your password has been changed."),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
