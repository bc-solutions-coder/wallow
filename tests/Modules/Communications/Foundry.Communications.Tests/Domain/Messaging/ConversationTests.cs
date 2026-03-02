using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Enums;
using Foundry.Communications.Domain.Messaging.Events;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Communications.Tests.Domain.Messaging;

public class ConversationTests
{
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Guid _userId1 = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();

    [Fact]
    public void CreateDirect_ReturnsConversationWithTwoParticipants_IsGroupFalse_NoSubject()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);

        conversation.Participants.Should().HaveCount(2);
        conversation.IsGroup.Should().BeFalse();
        conversation.Subject.Should().BeNull();
        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void CreateGroup_ReturnsConversationWithCorrectParticipantCount_IsGroupTrue_SubjectSet()
    {
        Guid member1 = Guid.NewGuid();
        Guid member2 = Guid.NewGuid();

        Conversation conversation = Conversation.CreateGroup(_tenantId, _userId1, "Team Chat", [member1, member2]);

        conversation.Participants.Should().HaveCount(3);
        conversation.IsGroup.Should().BeTrue();
        conversation.Subject.Should().Be("Team Chat");
        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void SendMessage_OnActiveConversation_RaisesMessageSentDomainEvent()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);
        conversation.ClearDomainEvents();

        conversation.SendMessage(_userId1, "Hello");

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MessageSentDomainEvent>();
    }

    [Fact]
    public void SendMessage_FromNonParticipant_ThrowsInvalidOperationException()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);
        Guid outsider = Guid.NewGuid();

        Action act = () => conversation.SendMessage(outsider, "Hello");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SendMessage_OnArchivedConversation_ThrowsInvalidOperationException()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);
        conversation.Archive();

        Action act = () => conversation.SendMessage(_userId1, "Hello");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkReadBy_UpdatesParticipantLastReadAt()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);

        conversation.MarkReadBy(_userId1);

        Participant participant = conversation.Participants.First(p => p.UserId == _userId1);
        participant.LastReadAt.Should().NotBeNull();
        participant.LastReadAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddParticipant_OnGroupConversation_RaisesParticipantAddedDomainEvent()
    {
        Conversation conversation = Conversation.CreateGroup(_tenantId, _userId1, "Group", [_userId2]);
        conversation.ClearDomainEvents();
        Guid newUser = Guid.NewGuid();

        conversation.AddParticipant(newUser);

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ParticipantAddedDomainEvent>();
        conversation.Participants.Should().HaveCount(3);
    }

    [Fact]
    public void AddParticipant_OnDirectConversation_ThrowsInvalidOperationException()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);
        Guid newUser = Guid.NewGuid();

        Action act = () => conversation.AddParticipant(newUser);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2);

        conversation.Archive();

        conversation.Status.Should().Be(ConversationStatus.Archived);
    }
}
