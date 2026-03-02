using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Enums;
using Foundry.Communications.Domain.Messaging.Identity;

namespace Foundry.Communications.Tests.Domain.Messaging;

public class MessageCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsMessageInSentStatus()
    {
        ConversationId conversationId = ConversationId.New();
        Guid senderId = Guid.NewGuid();
        string body = "Hello, world!";

        Message message = Message.Create(conversationId, senderId, body);

        message.ConversationId.Should().Be(conversationId);
        message.SenderId.Should().Be(senderId);
        message.Body.Should().Be(body);
        message.Status.Should().Be(MessageStatus.Sent);
        message.SentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }
}
