using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class OrganizationMemberRemovedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsRemovalEmailToMember()
    {
        Guid organizationId = Guid.NewGuid();

        OrganizationMemberRemovedEvent @event = new()
        {
            OrganizationId = organizationId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "removed@test.com"
        };

        await OrganizationMemberRemovedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "removed@test.com" &&
                cmd.Subject.Contains("removed from an organization") &&
                cmd.Body.Contains(organizationId.ToString())),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
