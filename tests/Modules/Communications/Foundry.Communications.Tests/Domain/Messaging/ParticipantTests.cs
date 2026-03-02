using Foundry.Communications.Domain.Messaging.Entities;
using Foundry.Communications.Domain.Messaging.Identity;

namespace Foundry.Communications.Tests.Domain.Messaging;

public class ParticipantCreateTests
{
    [Fact]
    public void Create_WithValidData_ReturnsActiveParticipant()
    {
        Guid userId = Guid.NewGuid();
        ConversationId conversationId = ConversationId.New();

        Participant participant = Participant.Create(userId, conversationId, TimeProvider.System);

        participant.UserId.Should().Be(userId);
        participant.ConversationId.Should().Be(conversationId);
        participant.IsActive.Should().BeTrue();
        participant.LastReadAt.Should().BeNull();
        participant.JoinedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }
}

public class ParticipantMarkReadTests
{
    [Fact]
    public void MarkRead_SetsLastReadAt()
    {
        Participant participant = Participant.Create(Guid.NewGuid(), ConversationId.New(), TimeProvider.System);

        participant.MarkRead(TimeProvider.System);

        participant.LastReadAt.Should().NotBeNull();
        participant.LastReadAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }
}

public class ParticipantLeaveTests
{
    [Fact]
    public void Leave_SetsIsActiveFalse()
    {
        Participant participant = Participant.Create(Guid.NewGuid(), ConversationId.New(), TimeProvider.System);

        participant.Leave();

        participant.IsActive.Should().BeFalse();
    }
}
