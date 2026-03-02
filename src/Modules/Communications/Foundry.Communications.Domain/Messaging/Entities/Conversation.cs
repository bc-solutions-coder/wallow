using Foundry.Communications.Domain.Exceptions;
using Foundry.Communications.Domain.Messaging.Enums;
using Foundry.Communications.Domain.Messaging.Events;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Messaging.Entities;

public sealed class Conversation : AggregateRoot<ConversationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string? Subject { get; private set; }
    public bool IsGroup { get; private set; }
    public ConversationStatus Status { get; private set; }

    private readonly List<Participant> _participants = [];
    public IReadOnlyList<Participant> Participants => _participants.AsReadOnly();

    private readonly List<Message> _messages = [];
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    private Conversation(TenantId tenantId, bool isGroup, string? subject, TimeProvider timeProvider)
        : base(ConversationId.New())
    {
        TenantId = tenantId;
        IsGroup = isGroup;
        Subject = subject;
        Status = ConversationStatus.Active;
        SetCreated(timeProvider.GetUtcNow());
    }

    public static Conversation CreateDirect(TenantId tenantId, Guid initiatorId, Guid recipientId, TimeProvider timeProvider)
    {
        Conversation conversation = new(tenantId, isGroup: false, subject: null, timeProvider);

        conversation._participants.Add(Participant.Create(initiatorId, conversation.Id, timeProvider));
        conversation._participants.Add(Participant.Create(recipientId, conversation.Id, timeProvider));

        conversation.RaiseDomainEvent(new ConversationCreatedDomainEvent(
            conversation.Id.Value,
            tenantId.Value));

        return conversation;
    }

    public static Conversation CreateGroup(TenantId tenantId, Guid creatorId, string subject, IEnumerable<Guid> memberIds, TimeProvider timeProvider)
    {
        Conversation conversation = new(tenantId, isGroup: true, subject: subject, timeProvider);

        conversation._participants.Add(Participant.Create(creatorId, conversation.Id, timeProvider));

        foreach (Guid memberId in memberIds.Distinct().Where(id => id != creatorId))
        {
            conversation._participants.Add(Participant.Create(memberId, conversation.Id, timeProvider));
        }

        conversation.RaiseDomainEvent(new ConversationCreatedDomainEvent(
            conversation.Id.Value,
            tenantId.Value));

        return conversation;
    }

    public void SendMessage(Guid senderId, string body, TimeProvider timeProvider)
    {
        if (Status == ConversationStatus.Archived)
        {
            throw new ConversationException("Cannot send messages to an archived conversation.");
        }

        Participant? sender = _participants.FirstOrDefault(p => p.UserId == senderId && p.IsActive);
        if (sender is null)
        {
            throw new ConversationException("Sender is not an active participant in this conversation.");
        }

        Message message = Message.Create(Id, senderId, body, timeProvider);
        _messages.Add(message);
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new MessageSentDomainEvent(
            Id.Value,
            message.Id.Value,
            senderId,
            TenantId.Value));
    }

    public void AddParticipant(Guid userId, TimeProvider timeProvider)
    {
        if (!IsGroup)
        {
            throw new ConversationException("Cannot add participants to a direct conversation.");
        }

        if (_participants.Any(p => p.UserId == userId))
        {
            throw new ConversationException("User is already a participant in this conversation.");
        }

        Participant participant = Participant.Create(userId, Id, timeProvider);
        _participants.Add(participant);
        SetUpdated(timeProvider.GetUtcNow());

        RaiseDomainEvent(new ParticipantAddedDomainEvent(
            Id.Value,
            userId,
            TenantId.Value));
    }

    public void MarkReadBy(Guid userId, TimeProvider timeProvider)
    {
        Participant? participant = _participants.FirstOrDefault(p => p.UserId == userId && p.IsActive);
        participant?.MarkRead(timeProvider);
    }

    public void Archive(TimeProvider timeProvider)
    {
        Status = ConversationStatus.Archived;
        SetUpdated(timeProvider.GetUtcNow());
    }
}
