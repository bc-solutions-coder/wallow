using Wallow.Messaging.Application.Conversations.DTOs;

namespace Wallow.Messaging.Tests.Application.DTOs;

public class ParticipantDtoTests
{
    [Fact]
    public void ParticipantDto_CreatesWithAllProperties()
    {
        Guid userId = Guid.NewGuid();
        DateTime joinedAt = DateTime.UtcNow;
        DateTime lastReadAt = DateTime.UtcNow.AddMinutes(-5);

        ParticipantDto dto = new(userId, joinedAt, lastReadAt, true);

        dto.UserId.Should().Be(userId);
        dto.JoinedAt.Should().Be(joinedAt);
        dto.LastReadAt.Should().Be(lastReadAt);
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ParticipantDto_WithNullLastReadAt_IsValid()
    {
        ParticipantDto dto = new(Guid.NewGuid(), DateTime.UtcNow, null, false);

        dto.LastReadAt.Should().BeNull();
        dto.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ParticipantDto_Equality_WorksCorrectly()
    {
        Guid userId = Guid.NewGuid();
        DateTime joinedAt = DateTime.UtcNow;

        ParticipantDto dto1 = new(userId, joinedAt, null, true);
        ParticipantDto dto2 = new(userId, joinedAt, null, true);

        dto1.Should().Be(dto2);
    }
}
