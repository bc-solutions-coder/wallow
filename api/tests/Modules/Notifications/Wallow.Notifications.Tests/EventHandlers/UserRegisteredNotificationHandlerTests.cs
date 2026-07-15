using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Sms.Commands.SendSms;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class UserRegisteredNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IConfiguration _configuration;

    public UserRegisteredNotificationHandlerTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Branding:AppName"] = "TestApp"
            })
            .Build();
    }

    [Fact]
    public async Task Handle_WithPhoneNumber_SendsWelcomeSms()
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

        await UserRegisteredNotificationHandler.Handle(@event, _bus, _configuration);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendSmsCommand>(cmd =>
                cmd.To == "+1234567890" &&
                cmd.Body.Contains("John") &&
                cmd.Body.Contains("TestApp")),
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

        await UserRegisteredNotificationHandler.Handle(@event, _bus, _configuration);

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendSmsCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
