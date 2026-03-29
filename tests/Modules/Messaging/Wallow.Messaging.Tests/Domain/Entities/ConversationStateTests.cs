using System.Reflection;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Enums;
using Wallow.Messaging.Domain.Conversations.Events;
using Wallow.Messaging.Domain.Conversations.Exceptions;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Messaging.Tests.Domain.Entities;

public class ConversationStateTests
{
    private static Conversation CreateDirectConversation(out Guid initiatorId, out Guid recipientId)
    {
        initiatorId = Guid.NewGuid();
        recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(TenantId.New(), initiatorId, recipientId, TimeProvider.System);
        conversation.ClearDomainEvents();
        return conversation;
    }

    [Fact]
    public void SendMessage_WithActiveParticipant_AddsMessage()
    {
        Conversation conversation = CreateDirectConversation(out Guid senderId, out _);

        conversation.SendMessage(senderId, "Hello!", TimeProvider.System);

        conversation.Messages.Should().ContainSingle()
            .Which.Body.Should().Be("Hello!");
    }

    [Fact]
    public void SendMessage_RaisesMessageSentDomainEvent()
    {
        Conversation conversation = CreateDirectConversation(out Guid senderId, out _);

        conversation.SendMessage(senderId, "Hello!", TimeProvider.System);

        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MessageSentDomainEvent>()
            .Which.Should().Match<MessageSentDomainEvent>(e =>
                e.ConversationId == conversation.Id.Value &&
                e.SenderId == senderId &&
                e.TenantId == conversation.TenantId.Value);
    }

    [Fact]
    public void SendMessage_SetsUpdatedTimestamp()
    {
        Conversation conversation = CreateDirectConversation(out Guid senderId, out _);
        DateTime before = DateTime.UtcNow;

        conversation.SendMessage(senderId, "Hello!", TimeProvider.System);

        conversation.UpdatedAt.Should().NotBeNull();
        conversation.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void SendMessage_ToArchivedConversation_ThrowsConversationException()
    {
        Conversation conversation = CreateDirectConversation(out Guid senderId, out _);

        // Use reflection to set Status to Archived since no public Archive method exists
        PropertyInfo statusProperty = typeof(Conversation).GetProperty(nameof(Conversation.Status))!;
        statusProperty.SetValue(conversation, ConversationStatus.Archived);

        Action act = () => conversation.SendMessage(senderId, "Hello!", TimeProvider.System);

        act.Should().Throw<ConversationException>()
            .WithMessage("*archived conversation*");
    }

    [Fact]
    public void SendMessage_ByNonParticipant_ThrowsConversationException()
    {
        Conversation conversation = CreateDirectConversation(out _, out _);
        Guid nonParticipantId = Guid.NewGuid();

        Action act = () => conversation.SendMessage(nonParticipantId, "Hello!", TimeProvider.System);

        act.Should().Throw<ConversationException>()
            .WithMessage("*not an active participant*");
    }

    [Fact]
    public void MarkReadBy_WithActiveParticipant_SetsLastReadAt()
    {
        Conversation conversation = CreateDirectConversation(out Guid userId, out _);
        DateTimeOffset before = DateTimeOffset.UtcNow;

        conversation.MarkReadBy(userId, TimeProvider.System);

        Participant participant = conversation.Participants.Single(p => p.UserId == userId);
        participant.LastReadAt.Should().NotBeNull();
        participant.LastReadAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void MarkReadBy_WithNonParticipant_DoesNotThrow()
    {
        Conversation conversation = CreateDirectConversation(out _, out _);
        Guid nonParticipantId = Guid.NewGuid();

        Action act = () => conversation.MarkReadBy(nonParticipantId, TimeProvider.System);

        act.Should().NotThrow();
    }

    [Fact]
    public void MarkReadBy_WithInactiveParticipant_DoesNotUpdateLastReadAt()
    {
        Conversation conversation = CreateDirectConversation(out _, out Guid recipientId);
        Participant recipient = conversation.Participants.Single(p => p.UserId == recipientId);
        recipient.Leave();

        conversation.MarkReadBy(recipientId, TimeProvider.System);

        recipient.LastReadAt.Should().BeNull();
    }

    [Fact]
    public void SendMessage_MultipleMessages_AccumulatesInOrder()
    {
        Conversation conversation = CreateDirectConversation(out Guid senderId, out _);

        conversation.SendMessage(senderId, "First", TimeProvider.System);
        conversation.SendMessage(senderId, "Second", TimeProvider.System);

        conversation.Messages.Should().HaveCount(2);
        conversation.Messages[0].Body.Should().Be("First");
        conversation.Messages[1].Body.Should().Be("Second");
    }
}
