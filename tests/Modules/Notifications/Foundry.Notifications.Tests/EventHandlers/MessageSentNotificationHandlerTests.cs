using Foundry.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Foundry.Notifications.Application.EventHandlers;
using Foundry.Shared.Contracts.Messaging.Events;
using Wolverine;

namespace Foundry.Notifications.Tests.EventHandlers;

public class MessageSentNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_DispatchesNotificationsToAllParticipantsExceptSender()
    {
        Guid senderId = Guid.NewGuid();
        Guid participant1 = Guid.NewGuid();
        Guid participant2 = Guid.NewGuid();

        MessageSentIntegrationEvent @event = new()
        {
            ConversationId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            SenderId = senderId,
            Content = "Hello everyone!",
            SentAt = DateTimeOffset.UtcNow,
            TenantId = Guid.NewGuid(),
            ParticipantIds = [senderId, participant1, participant2]
        };

        await MessageSentNotificationHandler.Handle(@event, _bus);

        await _bus.Received(2).InvokeAsync(
            Arg.Any<SendNotificationCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == senderId),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == participant1),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == participant2),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
