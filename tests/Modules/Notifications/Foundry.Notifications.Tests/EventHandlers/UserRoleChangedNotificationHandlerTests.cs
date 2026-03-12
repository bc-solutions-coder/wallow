using Foundry.Notifications.Application.Channels.Email.Commands.SendEmail;
using Foundry.Notifications.Application.EventHandlers;
using Foundry.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Foundry.Notifications.Tests.EventHandlers;

public class UserRoleChangedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsRoleChangeEmailToUser()
    {
        UserRoleChangedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            OldRole = "Member",
            NewRole = "Admin"
        };

        await UserRoleChangedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "user@test.com" &&
                cmd.Subject.Contains("Role Has Been Updated") &&
                cmd.Body.Contains("Member") &&
                cmd.Body.Contains("Admin")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
