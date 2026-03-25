using Microsoft.Extensions.Logging;
using Wallow.Messaging.Application.Conversations.EventHandlers;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Events;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Contracts.Messaging.Events;
using Wallow.Shared.Kernel.Identity;
using Wolverine;

namespace Wallow.Messaging.Tests.Application.EventHandlers;

public class ConversationCreatedEventHandlerTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ILogger<ConversationCreatedEventHandler> _logger;

    public ConversationCreatedEventHandlerTests()
    {
        _logger = Substitute.For<ILogger<ConversationCreatedEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        TenantId tenantId = TenantId.New();
        Guid initiatorId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(tenantId, initiatorId, recipientId, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        ConversationCreatedDomainEvent domainEvent = new(conversation.Id.Value, tenantId.Value);

        await ConversationCreatedEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        await _bus.Received(1).PublishAsync(Arg.Is<ConversationCreatedIntegrationEvent>(e =>
            e.ConversationId == conversation.Id.Value &&
            e.TenantId == tenantId.Value &&
            e.ParticipantIds.Count == 2));
    }

    [Fact]
    public async Task HandleAsync_WhenConversationNotFound_DoesNotPublish()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        ConversationCreatedDomainEvent domainEvent = new(Guid.NewGuid(), Guid.NewGuid());

        await ConversationCreatedEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<ConversationCreatedIntegrationEvent>());
    }
}
