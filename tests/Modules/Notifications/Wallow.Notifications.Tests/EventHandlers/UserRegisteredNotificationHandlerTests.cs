using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Sms.Commands.SendSms;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class UserRegisteredNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_WithEmail_SendsWelcomeEmail()
    {
        UserRegisteredEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        await UserRegisteredNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "user@test.com" &&
                cmd.Subject == "Welcome to Wallow"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_WithPhoneNumber_SendsWelcomeEmailAndSms()
    {
        UserRegisteredEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "+1234567890"
        };

        await UserRegisteredNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd => cmd.To == "user@test.com"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendSmsCommand>(cmd => cmd.To == "+1234567890"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_WithoutPhoneNumber_DoesNotSendSms()
    {
        UserRegisteredEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        await UserRegisteredNotificationHandler.Handle(@event, _bus);

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendSmsCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
