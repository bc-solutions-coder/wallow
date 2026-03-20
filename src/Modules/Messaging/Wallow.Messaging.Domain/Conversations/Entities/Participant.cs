using Wallow.Messaging.Domain.Conversations.Identity;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Messaging.Domain.Conversations.Entities;

public sealed class Participant : Entity<ParticipantId>
{
    public Guid UserId { get; private set; }
    public ConversationId ConversationId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }
    public bool IsActive { get; private set; }

    // ReSharper disable once UnusedMember.Local
    private Participant() { } // EF Core

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
