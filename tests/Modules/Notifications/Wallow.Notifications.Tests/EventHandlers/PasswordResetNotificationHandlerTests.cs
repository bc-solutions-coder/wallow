using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class PasswordResetNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsCriticalPasswordResetEmail()
    {
        PasswordResetRequestedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            ResetToken = "reset-token-123"
        };

        await PasswordResetNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "user@test.com" &&
                cmd.Subject == "Password Reset Request" &&
                cmd.Body.Contains("reset-token-123")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
