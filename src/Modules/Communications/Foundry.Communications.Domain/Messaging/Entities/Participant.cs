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
        ConversationId conversationId,
        TimeProvider timeProvider)
        : base(ParticipantId.New())
    {
        UserId = userId;
        ConversationId = conversationId;
        JoinedAt = timeProvider.GetUtcNow();
        IsActive = true;
    }

    public static Participant Create(Guid userId, ConversationId conversationId, TimeProvider timeProvider)
    {
        return new Participant(userId, conversationId, timeProvider);
    }

    public void MarkRead(TimeProvider timeProvider)
    {
        LastReadAt = timeProvider.GetUtcNow();
    }

    public void Leave()
    {
        IsActive = false;
    }
}
