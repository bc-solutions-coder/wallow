using Foundry.Communications.Domain.Messaging.Enums;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Messaging.Entities;

public sealed class Message : Entity<MessageId>
{
    public ConversationId ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = null!;
    public DateTimeOffset SentAt { get; private set; }
    public MessageStatus Status { get; private set; }

    private Message() { }

    private Message(ConversationId conversationId, Guid senderId, string body)
        : base(MessageId.New())
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Body = body;
        SentAt = DateTimeOffset.UtcNow;
        Status = MessageStatus.Sent;
    }

    public static Message Create(ConversationId conversationId, Guid senderId, string body)
    {
        return new Message(conversationId, senderId, body);
    }
}
