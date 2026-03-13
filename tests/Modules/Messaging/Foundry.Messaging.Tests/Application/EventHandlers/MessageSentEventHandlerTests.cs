using Foundry.Messaging.Application.Conversations.EventHandlers;
using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Events;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Contracts.Messaging.Events;
using Foundry.Shared.Kernel.Identity;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Messaging.Tests.Application.EventHandlers;

public class MessageSentEventHandlerTests
{
    private readonly IConversationRepository _repository = Substitute.For<IConversationRepository>();
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ILogger<MessageSentEventHandler> _logger;

    public MessageSentEventHandlerTests()
    {
        _logger = Substitute.For<ILogger<MessageSentEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_PublishesIntegrationEvent()
    {
        TenantId tenantId = TenantId.New();
        Guid senderId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, recipientId, TimeProvider.System);
        conversation.SendMessage(senderId, "Hello!", TimeProvider.System);
        Message message = conversation.Messages[^1];

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        MessageSentDomainEvent domainEvent = new(
            conversation.Id.Value, message.Id.Value, senderId, tenantId.Value);

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        await _bus.Received(1).PublishAsync(Arg.Is<MessageSentIntegrationEvent>(e =>
            e.ConversationId == conversation.Id.Value &&
            e.MessageId == message.Id.Value &&
            e.SenderId == senderId &&
            e.Content == "Hello!" &&
            e.TenantId == tenantId.Value));
    }

    [Fact]
    public async Task HandleAsync_ExcludesSenderFromParticipantIds()
    {
        TenantId tenantId = TenantId.New();
        Guid senderId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, recipientId, TimeProvider.System);
        conversation.SendMessage(senderId, "Hi", TimeProvider.System);
        Message message = conversation.Messages[^1];

        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        MessageSentDomainEvent domainEvent = new(
            conversation.Id.Value, message.Id.Value, senderId, tenantId.Value);

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        await _bus.Received(1).PublishAsync(Arg.Is<MessageSentIntegrationEvent>(e =>
            e.ParticipantIds.Contains(recipientId) &&
            !e.ParticipantIds.Contains(senderId)));
    }

    [Fact]
    public async Task HandleAsync_WhenConversationNotFound_DoesNotPublish()
    {
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns((Conversation?)null);

        MessageSentDomainEvent domainEvent = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<MessageSentIntegrationEvent>());
    }

    [Fact]
    public async Task HandleAsync_WhenMessageNotFoundInConversation_DoesNotPublish()
    {
        TenantId tenantId = TenantId.New();
        Guid senderId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(tenantId, senderId, Guid.NewGuid(), TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ConversationId>(), Arg.Any<CancellationToken>())
            .Returns(conversation);

        // Use a messageId that doesn't exist in the conversation
        MessageSentDomainEvent domainEvent = new(
            conversation.Id.Value, Guid.NewGuid(), senderId, tenantId.Value);

        await MessageSentEventHandler.HandleAsync(
            domainEvent, _repository, _bus, _logger, CancellationToken.None);

        await _bus.DidNotReceive().PublishAsync(Arg.Any<MessageSentIntegrationEvent>());
    }
}
