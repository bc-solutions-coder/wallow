using Microsoft.Extensions.Logging;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Events;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Contracts.Messaging.Events;
using Wolverine;

namespace Wallow.Messaging.Application.Conversations.EventHandlers;

public sealed partial class ConversationCreatedEventHandler
{
    public static async Task HandleAsync(
        ConversationCreatedDomainEvent domainEvent,
        IConversationRepository conversationRepository,
        IMessageBus bus,
        ILogger<ConversationCreatedEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingConversationCreated(logger, domainEvent.ConversationId);

        Conversation? conversation = await conversationRepository.GetByIdAsync(
            ConversationId.Create(domainEvent.ConversationId), cancellationToken);

        if (conversation is null)
        {
            LogConversationNotFound(logger, domainEvent.ConversationId);
            return;
        }

        ConversationCreatedIntegrationEvent integrationEvent = new()
        {
            ConversationId = domainEvent.ConversationId,
            ParticipantIds = conversation.Participants.Select(p => p.UserId).ToList(),
            CreatedAt = new DateTimeOffset(conversation.CreatedAt, TimeSpan.Zero),
            TenantId = domainEvent.TenantId
        };

        await bus.PublishAsync(integrationEvent);

        LogIntegrationEventPublished(logger, domainEvent.ConversationId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling ConversationCreatedDomainEvent for Conversation {ConversationId}")]
    private static partial void LogHandlingConversationCreated(ILogger logger, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation {ConversationId} not found while handling ConversationCreatedDomainEvent")]
    private static partial void LogConversationNotFound(ILogger logger, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published ConversationCreatedIntegrationEvent for Conversation {ConversationId}")]
    private static partial void LogIntegrationEventPublished(ILogger logger, Guid conversationId);
}
