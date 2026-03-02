using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Application.Messaging.Interfaces;
using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Events;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Contracts.Communications.Messaging.Events;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Foundry.Communications.Application.Messaging.EventHandlers;

public sealed partial class MessageSentEventHandler
{
    public static async Task HandleAsync(
        MessageSentDomainEvent domainEvent,
        IConversationRepository conversationRepository,
        INotificationService notificationService,
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

        string title = conversation.IsGroup && conversation.Subject is not null
            ? $"New message in {conversation.Subject}"
            : "New message";

        IEnumerable<Guid> recipientIds = conversation.Participants
            .Where(p => p.IsActive && p.UserId != domainEvent.SenderId)
            .Select(p => p.UserId);

        foreach (Guid recipientId in recipientIds)
        {
            await notificationService.SendToUserAsync(
                recipientId,
                title,
                "You have a new message.",
                "Message",
                cancellationToken);
        }

        LogNotificationsSent(logger, domainEvent.MessageId, domainEvent.ConversationId);

        Message? message = conversation.Messages
            .FirstOrDefault(m => m.Id == new MessageId(domainEvent.MessageId));

        if (message is not null)
        {
            await bus.PublishAsync(new MessageSentIntegrationEvent
            {
                ConversationId = domainEvent.ConversationId,
                MessageId = domainEvent.MessageId,
                SenderId = domainEvent.SenderId,
                Content = message.Body,
                SentAt = message.SentAt,
                TenantId = domainEvent.TenantId
            });
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling MessageSentDomainEvent for Message {MessageId} in Conversation {ConversationId}")]
    private static partial void LogHandlingMessageSent(ILogger logger, Guid messageId, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conversation {ConversationId} not found while handling MessageSentDomainEvent")]
    private static partial void LogConversationNotFound(ILogger logger, Guid conversationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notifications sent for Message {MessageId} in Conversation {ConversationId}")]
    private static partial void LogNotificationsSent(ILogger logger, Guid messageId, Guid conversationId);
}
