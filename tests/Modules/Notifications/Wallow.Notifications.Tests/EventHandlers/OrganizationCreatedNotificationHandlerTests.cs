using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class OrganizationCreatedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_SendsWelcomeEmailToCreator()
    {
        OrganizationCreatedEvent @event = new()
        {
            OrganizationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Acme Corp",
            CreatorEmail = "founder@test.com"
        };

        await OrganizationCreatedNotificationHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "founder@test.com" &&
                cmd.Subject.Contains("Acme Corp") &&
                cmd.Body.Contains("Acme Corp")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
