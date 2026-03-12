using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Messaging.Domain.Conversations.Enums;
using Foundry.Messaging.Domain.Conversations.Events;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Messaging.Tests.Domain.Entities;

public class MessageCreateTests
{
    [Fact]
    public void Create_ViaConversationSendMessage_SetsAllProperties()
    {
        Guid senderId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(TenantId.New(), senderId, Guid.NewGuid(), TimeProvider.System);
        conversation.ClearDomainEvents();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        conversation.SendMessage(senderId, "Test message body", TimeProvider.System);

        Message message = conversation.Messages.Single();
        message.ConversationId.Should().Be(conversation.Id);
        message.SenderId.Should().Be(senderId);
        message.Body.Should().Be("Test message body");
        message.Status.Should().Be(MessageStatus.Sent);
        message.SentAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Create_ViaConversationSendMessage_RaisesMessageSentDomainEvent()
    {
        Guid senderId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(TenantId.New(), senderId, Guid.NewGuid(), TimeProvider.System);
        conversation.ClearDomainEvents();

        conversation.SendMessage(senderId, "Hello", TimeProvider.System);

        Message message = conversation.Messages.Single();
        conversation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<MessageSentDomainEvent>()
            .Which.Should().Match<MessageSentDomainEvent>(e =>
                e.MessageId == message.Id.Value &&
                e.SenderId == senderId &&
                e.ConversationId == conversation.Id.Value);
    }

    [Fact]
    public void Create_ViaConversationSendMessage_DefaultStatusIsSent()
    {
        Guid senderId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(TenantId.New(), senderId, Guid.NewGuid(), TimeProvider.System);

        conversation.SendMessage(senderId, "Test", TimeProvider.System);

        conversation.Messages.Single().Status.Should().Be(MessageStatus.Sent);
    }

    [Fact]
    public void Create_ViaConversationSendMessage_AssignsUniqueMessageId()
    {
        Guid senderId = Guid.NewGuid();
        Conversation conversation = Conversation.CreateDirect(TenantId.New(), senderId, Guid.NewGuid(), TimeProvider.System);

        conversation.SendMessage(senderId, "First", TimeProvider.System);
        conversation.SendMessage(senderId, "Second", TimeProvider.System);

        conversation.Messages[0].Id.Should().NotBe(conversation.Messages[1].Id);
    }
}
