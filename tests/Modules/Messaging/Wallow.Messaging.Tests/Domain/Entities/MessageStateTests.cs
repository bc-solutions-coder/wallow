using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Messaging.Domain.Conversations.Enums;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Messaging.Tests.Domain.Entities;

public class MessageStateTests
{
    private static Conversation CreateConversationWithMessage(out Guid senderId, out Guid recipientId)
    {
        senderId = Guid.NewGuid();
        recipientId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(TenantId.New(), senderId, recipientId, TimeProvider.System);
        conversation.ClearDomainEvents();
        conversation.SendMessage(senderId, "Test message", TimeProvider.System);
        conversation.ClearDomainEvents();
        return conversation;
    }

    [Fact]
    public void Message_InitialStatus_IsSent()
    {
        Conversation conversation = CreateConversationWithMessage(out _, out _);

        conversation.Messages.Single().Status.Should().Be(MessageStatus.Sent);
    }

    [Fact]
    public void Participant_InitialLastReadAt_IsNull()
    {
        Conversation conversation = CreateConversationWithMessage(out _, out Guid recipientId);

        Participant recipient = conversation.Participants.Single(p => p.UserId == recipientId);
        recipient.LastReadAt.Should().BeNull();
    }

    [Fact]
    public void Participant_MarkRead_SetsLastReadAt()
    {
        Conversation conversation = CreateConversationWithMessage(out _, out Guid recipientId);
        DateTimeOffset before = DateTimeOffset.UtcNow;

        conversation.MarkReadBy(recipientId, TimeProvider.System);

        Participant recipient = conversation.Participants.Single(p => p.UserId == recipientId);
        recipient.LastReadAt.Should().NotBeNull();
        recipient.LastReadAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Participant_Leave_SetsIsActiveToFalse()
    {
        Conversation conversation = CreateConversationWithMessage(out _, out Guid recipientId);
        Participant recipient = conversation.Participants.Single(p => p.UserId == recipientId);

        recipient.Leave();

        recipient.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Participant_Leave_ThenSendMessage_ThrowsException()
    {
        Conversation conversation = CreateConversationWithMessage(out _, out Guid recipientId);
        Participant recipient = conversation.Participants.Single(p => p.UserId == recipientId);
        recipient.Leave();

        Action act = () => conversation.SendMessage(recipientId, "After leaving", TimeProvider.System);

        act.Should().Throw<Exception>()
            .WithMessage("*not an active participant*");
    }

    [Fact]
    public void Participant_MarkRead_UpdatesTimestampOnSubsequentCalls()
    {
        Conversation conversation = CreateConversationWithMessage(out _, out Guid recipientId);

        conversation.MarkReadBy(recipientId, TimeProvider.System);
        DateTimeOffset? firstReadAt = conversation.Participants.Single(p => p.UserId == recipientId).LastReadAt;

        conversation.MarkReadBy(recipientId, TimeProvider.System);
        DateTimeOffset? secondReadAt = conversation.Participants.Single(p => p.UserId == recipientId).LastReadAt;

        secondReadAt.Should().BeOnOrAfter(firstReadAt!.Value);
    }
}
