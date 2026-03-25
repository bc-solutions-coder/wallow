using Wallow.Messaging.Domain.Conversations.Identity;

namespace Wallow.Messaging.Tests.Domain.Identity;

public class MessageIdTests
{
    [Fact]
    public void Create_WithGuid_ReturnsMessageIdWithSameValue()
    {
        Guid value = Guid.NewGuid();

        MessageId id = MessageId.Create(value);

        id.Value.Should().Be(value);
    }
}
