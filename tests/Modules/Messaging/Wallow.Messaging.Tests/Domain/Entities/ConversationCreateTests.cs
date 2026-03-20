using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Enums;
using Wallow.Messaging.Domain.Conversations.Events;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Messaging.Tests.Domain.Entities;

public class ConversationCreateTests
{
    [Fact]
    public void CreateDirect_WithValidData_ReturnsActiveConversation()
    {
        TenantId tenantId = TenantId.New();
        Guid initiatorId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();

        Conversation conversation = Conversation.CreateDirect(tenantId, initiatorId, recipientId, TimeProvider.System);

        conversation.TenantId.Should().Be(tenantId);
        conversation.IsGroup.Should().BeFalse();
        conversation.Subject.Should().BeNull();
        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void CreateDirect_AddsTwoParticipants()
    {
        TenantId tenantId = TenantId.New();
        Guid initiatorId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();

        Conversation conversation = Conversation.CreateDirect(tenantId, initiatorId, recipientId, TimeProvider.System);

        conversation.Participants.Should().HaveCount(2);
        conversation.Participants.Select(p => p.UserId).Should().Contain(initiatorId);
        conversation.Participants.Select(p => p.UserId).Should().Contain(recipientId);
    }

    [Fact]
    public void CreateDirect_RaisesConversationCreatedDomainEvent()
    {
        TenantId tenantId = TenantId.New();
        Guid initiatorId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();

        Conversation conversation = Conversation.CreateDirect(tenantId, initiatorId, recipientId, TimeProvider.System);

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConversationCreatedDomainEvent>()
            .Which.Should().Match<ConversationCreatedDomainEvent>(e =>
                e.ConversationId == conversation.Id.Value &&
                e.TenantId == tenantId.Value);
    }

    [Fact]
    public void CreateDirect_SetsCreatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;

        Conversation conversation = Conversation.CreateDirect(TenantId.New(), Guid.NewGuid(), Guid.NewGuid(), TimeProvider.System);

        conversation.CreatedAt.Should().BeOnOrAfter(before);
        conversation.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void CreateGroup_WithValidData_ReturnsActiveGroupConversation()
    {
        TenantId tenantId = TenantId.New();
        Guid creatorId = Guid.NewGuid();
        string subject = "Project Discussion";
        List<Guid> memberIds = [Guid.NewGuid(), Guid.NewGuid()];

        Conversation conversation = Conversation.CreateGroup(tenantId, creatorId, subject, memberIds, TimeProvider.System);

        conversation.TenantId.Should().Be(tenantId);
        conversation.IsGroup.Should().BeTrue();
        conversation.Subject.Should().Be(subject);
        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void CreateGroup_AddsCreatorAndMembers()
    {
        Guid creatorId = Guid.NewGuid();
        Guid member1 = Guid.NewGuid();
        Guid member2 = Guid.NewGuid();

        Conversation conversation = Conversation.CreateGroup(TenantId.New(), creatorId, "Subject", [member1, member2], TimeProvider.System);

        conversation.Participants.Should().HaveCount(3);
        conversation.Participants.Select(p => p.UserId).Should().Contain(creatorId);
        conversation.Participants.Select(p => p.UserId).Should().Contain(member1);
        conversation.Participants.Select(p => p.UserId).Should().Contain(member2);
    }

    [Fact]
    public void CreateGroup_DeduplicatesCreatorFromMembers()
    {
        Guid creatorId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        Conversation conversation = Conversation.CreateGroup(TenantId.New(), creatorId, "Subject", [creatorId, memberId], TimeProvider.System);

        conversation.Participants.Should().HaveCount(2);
        conversation.Participants.Where(p => p.UserId == creatorId).Should().HaveCount(1);
    }

    [Fact]
    public void CreateGroup_DeduplicatesDuplicateMembers()
    {
        Guid creatorId = Guid.NewGuid();
        Guid memberId = Guid.NewGuid();

        Conversation conversation = Conversation.CreateGroup(TenantId.New(), creatorId, "Subject", [memberId, memberId], TimeProvider.System);

        conversation.Participants.Should().HaveCount(2);
    }

    [Fact]
    public void CreateGroup_RaisesConversationCreatedDomainEvent()
    {
        TenantId tenantId = TenantId.New();

        Conversation conversation = Conversation.CreateGroup(tenantId, Guid.NewGuid(), "Subject", [Guid.NewGuid()], TimeProvider.System);

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ConversationCreatedDomainEvent>()
            .Which.Should().Match<ConversationCreatedDomainEvent>(e =>
                e.ConversationId == conversation.Id.Value &&
                e.TenantId == tenantId.Value);
    }

    [Fact]
    public void CreateGroup_AllParticipantsAreActive()
    {
        Conversation conversation = Conversation.CreateGroup(TenantId.New(), Guid.NewGuid(), "Subject", [Guid.NewGuid()], TimeProvider.System);

        conversation.Participants.Should().AllSatisfy(p => p.IsActive.Should().BeTrue());
    }
}
