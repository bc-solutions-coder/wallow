using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Tests.Common.Builders;

public class ConversationBuilder
{
    private TenantId _tenantId = TenantId.New();
    private Guid _initiatorId = Guid.NewGuid();
    private Guid _recipientId = Guid.NewGuid();
    private bool _isGroup;
    private string _subject = "Test Group";
    private readonly List<Guid> _additionalMembers = [];
    private readonly List<string> _messages = [];
    private bool _archived;

    public ConversationBuilder WithTenantId(TenantId tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public ConversationBuilder WithInitiatorId(Guid initiatorId)
    {
        _initiatorId = initiatorId;
        return this;
    }

    public ConversationBuilder WithRecipientId(Guid recipientId)
    {
        _recipientId = recipientId;
        return this;
    }

    public ConversationBuilder AsGroup(string subject)
    {
        _isGroup = true;
        _subject = subject;
        return this;
    }

    public ConversationBuilder WithMember(Guid memberId)
    {
        _additionalMembers.Add(memberId);
        return this;
    }

    public ConversationBuilder WithMessage(string body)
    {
        _messages.Add(body);
        return this;
    }

    public ConversationBuilder AsArchived()
    {
        _archived = true;
        return this;
    }

    public Conversation Build()
    {
        Conversation conversation = _isGroup
            ? Conversation.CreateGroup(_tenantId, _initiatorId, _subject, [_recipientId, .._additionalMembers])
            : Conversation.CreateDirect(_tenantId, _initiatorId, _recipientId);

        foreach (string body in _messages)
        {
            conversation.SendMessage(_initiatorId, body);
        }

        if (_archived)
        {
            conversation.Archive();
        }

        conversation.ClearDomainEvents();

        return conversation;
    }

    public static ConversationBuilder Create() => new();
}
