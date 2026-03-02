using Foundry.Communications.Domain.Messaging.Enums;
using Foundry.Communications.Domain.Messaging.Events;
using Foundry.Communications.Domain.Messaging.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Communications.Domain.Messaging.Entities;

public sealed class Conversation : AggregateRoot<ConversationId>, ITenantScoped
{
    public TenantId TenantId { get; set; }
    public string? Subject { get; private set; }
    public bool IsGroup { get; private set; }
    public ConversationStatus Status { get; private set; }

    private readonly List<Participant> _participants = [];
    public IReadOnlyList<Participant> Participants => _participants.AsReadOnly();

    private readonly List<Message> _messages = [];
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    private Conversation() { }

    private Conversation(TenantId tenantId, bool isGroup, string? subject)
        : base(ConversationId.New())
    {
        TenantId = tenantId;
        IsGroup = isGroup;
        Subject = subject;
        Status = ConversationStatus.Active;
        SetCreated();
    }

    public static Conversation CreateDirect(TenantId tenantId, Guid initiatorId, Guid recipientId)
    {
        Conversation conversation = new(tenantId, isGroup: false, subject: null);

        conversation._participants.Add(Participant.Create(initiatorId, conversation.Id));
        conversation._participants.Add(Participant.Create(recipientId, conversation.Id));

        conversation.RaiseDomainEvent(new ConversationCreatedDomainEvent(
            conversation.Id.Value,
            tenantId.Value));

        return conversation;
    }

    public static Conversation CreateGroup(TenantId tenantId, Guid creatorId, string subject, IEnumerable<Guid> memberIds)
    {
        Conversation conversation = new(tenantId, isGroup: true, subject: subject);

        conversation._participants.Add(Participant.Create(creatorId, conversation.Id));

        foreach (Guid memberId in memberIds)
        {
            conversation._participants.Add(Participant.Create(memberId, conversation.Id));
        }

        conversation.RaiseDomainEvent(new ConversationCreatedDomainEvent(
            conversation.Id.Value,
            tenantId.Value));

        return conversation;
    }

    public void SendMessage(Guid senderId, string body)
    {
        if (Status == ConversationStatus.Archived)
            throw new InvalidOperationException("Cannot send messages to an archived conversation.");

        Participant? sender = _participants.FirstOrDefault(p => p.UserId == senderId && p.IsActive);
        if (sender is null)
            throw new InvalidOperationException("Sender is not an active participant in this conversation.");

        Message message = Message.Create(Id, senderId, body);
        _messages.Add(message);
        SetUpdated();

        RaiseDomainEvent(new MessageSentDomainEvent(
            Id.Value,
            message.Id.Value,
            senderId,
            TenantId.Value));
    }

    public void AddParticipant(Guid userId)
    {
        if (!IsGroup)
            throw new InvalidOperationException("Cannot add participants to a direct conversation.");

        Participant participant = Participant.Create(userId, Id);
        _participants.Add(participant);
        SetUpdated();

        RaiseDomainEvent(new ParticipantAddedDomainEvent(
            Id.Value,
            userId,
            TenantId.Value));
    }

    public void MarkReadBy(Guid userId)
    {
        Participant? participant = _participants.FirstOrDefault(p => p.UserId == userId && p.IsActive);
        participant?.MarkRead();
    }

    public void Archive()
    {
        Status = ConversationStatus.Archived;
        SetUpdated();
    }
}
