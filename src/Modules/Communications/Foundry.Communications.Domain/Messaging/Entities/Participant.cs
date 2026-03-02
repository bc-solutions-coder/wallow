using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Messaging.Entities;

public sealed class Participant : Entity<ParticipantId>
{
    public Guid UserId { get; private set; }
    public ConversationId ConversationId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }
    public bool IsActive { get; private set; }

    private Participant() { }

    private Participant(
        Guid userId,
        ConversationId conversationId)
        : base(ParticipantId.New())
    {
        UserId = userId;
        ConversationId = conversationId;
        JoinedAt = DateTimeOffset.UtcNow;
        IsActive = true;
    }

    public static Participant Create(Guid userId, ConversationId conversationId)
    {
        return new Participant(userId, conversationId);
    }

    public void MarkRead()
    {
        LastReadAt = DateTimeOffset.UtcNow;
    }

    public void Leave()
    {
        IsActive = false;
    }
}
