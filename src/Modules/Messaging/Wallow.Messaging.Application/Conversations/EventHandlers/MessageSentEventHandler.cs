using Microsoft.Extensions.Logging;
using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Events;
using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Contracts.Messaging.Events;
using Wolverine;

namespace Wallow.Messaging.Application.Conversations.EventHandlers;

public sealed partial class MessageSentEventHandler
{
    public static async Task HandleAsync(
        MessageSentDomainEvent domainEvent,
        IConversationRepository conversationRepository,
        IMessageBus bus,
        ILogger<MessageSentEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingMessageSent(logger, domainEvent.MessageId, domainEvent.ConversationId);

        Conversation? conversation = await conversationRepository.GetByIdAsync(
            new ConversationId(domainEvent.ConversationId), cancellationToken);

        if (conversation is null)
        {
            LogConversationNotFound(logger, domainEvent.ConversationId);
            return;
        }

        Message? message = conversation.Messages
            .FirstOrDefault(m => m.Id == new MessageId(domainEvent.MessageId));

        if (message is null)
        {
            return;
        }

        List<Guid> participantIds = conversation.Participants
            .Where(p => p.IsActive && p.UserId != domainEvent.SenderId)
            .Select(p => p.UserId)
            .ToList();

        await bus.PublishAsync(new MessageSentIntegrationEvent
        {
            ConversationId = domainEvent.ConversationId,
            MessageId = domainEvent.MessageId,
            SenderId = domainEvent.SenderId,
            Content = message.Body,
            SentAt = message.SentAt,
            TenantId = domainEvent.TenantId,
            ParticipantIds = participantIds
        });

        LogIntegrationEventPublished(logger, domainEvent.MessageId, domainEvent.ConversationId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling MessageSentDomainEvent for Message {MessageId} in Conversation {ConversationId}")]
    private static partial void LogHandlingMessageSent(ILogger logger, Guid messageId, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation {ConversationId} not found while handling MessageSentDomainEvent")]
    private static partial void LogConversationNotFound(ILogger logger, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published MessageSentIntegrationEvent for Message {MessageId} in Conversation {ConversationId}")]
    private static partial void LogIntegrationEventPublished(ILogger logger, Guid messageId, Guid conversationId);
}
