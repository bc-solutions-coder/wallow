using Foundry.Messaging.Domain.Conversations.Enums;
using Foundry.Messaging.Domain.Conversations.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Messaging.Domain.Conversations.Entities;

public sealed class Message : Entity<MessageId>
{
    public ConversationId ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }
    public MessageStatus Status { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private Message() { } // EF Core

    private Message(ConversationId conversationId, Guid senderId, string body, TimeProvider timeProvider)
        : base(MessageId.New())
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Body = body;
        SentAt = timeProvider.GetUtcNow();
        Status = MessageStatus.Sent;
    }

    public static Message Create(ConversationId conversationId, Guid senderId, string body, TimeProvider timeProvider)
    {
        return new Message(conversationId, senderId, body, timeProvider);
    }
}
