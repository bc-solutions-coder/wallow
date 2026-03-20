using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class OrganizationMemberAddedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsWelcomeEmailToNewMember()
    {
        OrganizationMemberAddedEvent @event = new()
        {
            OrganizationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "newmember@test.com"
        };

        await OrganizationMemberAddedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "newmember@test.com" &&
                cmd.Subject.Contains("Added to an Organization")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
