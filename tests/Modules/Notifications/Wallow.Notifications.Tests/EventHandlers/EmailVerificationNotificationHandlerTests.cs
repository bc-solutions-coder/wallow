using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class EmailVerificationNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();

    [Fact]
    public async Task Handle_RendersTemplateAndSendsVerificationEmail()
    {
        EmailVerificationRequestedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "John",
            VerifyUrl = "http://localhost/verify?token=abc123"
        };

        string renderedHtml = "<p>Hi John, verify at http://localhost/verify?token=abc123</p>";
        _templateService.RenderAsync("emailverification", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(renderedHtml);

        await EmailVerificationNotificationHandler.Handle(@event, _templateService, _bus);

        await _templateService.Received(1).RenderAsync("emailverification", Arg.Any<object>(), Arg.Any<CancellationToken>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "user@test.com" &&
                cmd.Subject == "Verify your email address" &&
                cmd.Body == renderedHtml),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
