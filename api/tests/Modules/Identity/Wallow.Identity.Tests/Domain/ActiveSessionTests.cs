using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Tests.Domain;

public class ActiveSessionTests
{
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_SetsAllFieldsCorrectly()
    {
        TimeSpan duration = TimeSpan.FromHours(1);

        ActiveSession session = ActiveSession.Create(_userId, _tenantId, duration, _timeProvider);

        session.Id.Value.Should().NotBeEmpty();
        session.UserId.Should().Be(_userId);
        session.TenantId.Should().Be(_tenantId);
        session.SessionToken.Should().NotBeNullOrWhiteSpace();
        session.CreatedAt.Should().Be(_timeProvider.GetUtcNow());
        session.LastActivityAt.Should().Be(_timeProvider.GetUtcNow());
        session.ExpiresAt.Should().Be(_timeProvider.GetUtcNow().Add(duration));
        session.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_BeforeExpiresAt_ReturnsFalse()
    {
        ActiveSession session = ActiveSession.Create(_userId, _tenantId, TimeSpan.FromHours(1), _timeProvider);

        _timeProvider.Advance(TimeSpan.FromMinutes(30));

        bool result = session.IsExpired(_timeProvider);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AfterExpiresAt_ReturnsTrue()
    {
        ActiveSession session = ActiveSession.Create(_userId, _tenantId, TimeSpan.FromHours(1), _timeProvider);

        _timeProvider.Advance(TimeSpan.FromHours(2));

        bool result = session.IsExpired(_timeProvider);

        result.Should().BeTrue();
    }

    [Fact]
    public void Touch_UpdatesLastActivityAt()
    {
        ActiveSession session = ActiveSession.Create(_userId, _tenantId, TimeSpan.FromHours(1), _timeProvider);
        DateTimeOffset originalActivity = session.LastActivityAt;

        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        session.Touch(_timeProvider);

        session.LastActivityAt.Should().BeAfter(originalActivity);
        session.LastActivityAt.Should().Be(_timeProvider.GetUtcNow());
    }

    [Fact]
    public void Revoke_SetsIsRevokedTrue()
    {
        ActiveSession session = ActiveSession.Create(_userId, _tenantId, TimeSpan.FromHours(1), _timeProvider);

        session.Revoke();

        session.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Create_TwoSessions_ProducesDistinctSessionTokens()
    {
        ActiveSession session1 = ActiveSession.Create(_userId, _tenantId, TimeSpan.FromHours(1), _timeProvider);
        ActiveSession session2 = ActiveSession.Create(_userId, _tenantId, TimeSpan.FromHours(1), _timeProvider);

        session1.SessionToken.Should().NotBe(session2.SessionToken);
    }
}
