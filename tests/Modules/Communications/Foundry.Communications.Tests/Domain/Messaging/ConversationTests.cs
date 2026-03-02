using Foundry.Communications.Domain.Exceptions;
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
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);

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

        Conversation conversation = Conversation.CreateGroup(_tenantId, _userId1, "Team Chat", [member1, member2], TimeProvider.System);

        conversation.Participants.Should().HaveCount(3);
        conversation.IsGroup.Should().BeTrue();
        conversation.Subject.Should().Be("Team Chat");
        conversation.Status.Should().Be(ConversationStatus.Active);
    }

    [Fact]
    public void SendMessage_OnActiveConversation_RaisesMessageSentDomainEvent()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);
        conversation.ClearDomainEvents();

        conversation.SendMessage(_userId1, "Hello", TimeProvider.System);

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MessageSentDomainEvent>();
    }

    [Fact]
    public void SendMessage_FromNonParticipant_ThrowsInvalidOperationException()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);
        Guid outsider = Guid.NewGuid();

        Action act = () => conversation.SendMessage(outsider, "Hello", TimeProvider.System);

        act.Should().Throw<ConversationException>();
    }

    [Fact]
    public void SendMessage_OnArchivedConversation_ThrowsInvalidOperationException()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);
        conversation.Archive(TimeProvider.System);

        Action act = () => conversation.SendMessage(_userId1, "Hello", TimeProvider.System);

        act.Should().Throw<ConversationException>();
    }

    [Fact]
    public void MarkReadBy_UpdatesParticipantLastReadAt()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);

        conversation.MarkReadBy(_userId1, TimeProvider.System);

        Participant participant = conversation.Participants.First(p => p.UserId == _userId1);
        participant.LastReadAt.Should().NotBeNull();
        participant.LastReadAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddParticipant_OnGroupConversation_RaisesParticipantAddedDomainEvent()
    {
        Conversation conversation = Conversation.CreateGroup(_tenantId, _userId1, "Group", [_userId2], TimeProvider.System);
        conversation.ClearDomainEvents();
        Guid newUser = Guid.NewGuid();

        conversation.AddParticipant(newUser, TimeProvider.System);

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ParticipantAddedDomainEvent>();
        conversation.Participants.Should().HaveCount(3);
    }

    [Fact]
    public void AddParticipant_OnDirectConversation_ThrowsInvalidOperationException()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);
        Guid newUser = Guid.NewGuid();

        Action act = () => conversation.AddParticipant(newUser, TimeProvider.System);

        act.Should().Throw<ConversationException>();
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        Conversation conversation = Conversation.CreateDirect(_tenantId, _userId1, _userId2, TimeProvider.System);

        conversation.Archive(TimeProvider.System);

        conversation.Status.Should().Be(ConversationStatus.Archived);
    }
}
