using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class SessionServiceTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly IAccessRevoker _accessRevoker;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: $"Session_{Guid.NewGuid()}")
            .Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dp);

        _messageBus = Substitute.For<IMessageBus>();
        _accessRevoker = Substitute.For<IAccessRevoker>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _sut = new SessionService(
            _dbContext,
            _messageBus,
            _accessRevoker,
            _timeProvider,
            NullLogger<SessionService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // ──────────────────────────────────────────────
    // CreateSessionAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateSession_ReturnsNewSessionWithUniqueToken()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ActiveSession session = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        session.Should().NotBeNull();
        session.UserId.Should().Be(userId);
        session.TenantId.Should().Be(tenantId);
        session.SessionToken.Should().NotBeNullOrEmpty();
        session.IsRevoked.Should().BeFalse();
        session.Id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateSession_BelowMaxSessions_DoesNotEvict()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        // Create 4 sessions (below max of 5)
        for (int i = 0; i < 4; i++)
        {
            await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
        }

        // 5th session should NOT trigger eviction
        ActiveSession session = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        session.Should().NotBeNull();
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<UserSessionEvictedEvent>());
    }

    [Fact]
    public async Task CreateSession_BelowMaxSessions_RevokesNoTokens()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
        }

        await _accessRevoker.DidNotReceive().RevokeSessionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSession_Eviction_RevokesTheEvictedSessionsTokens()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ActiveSession oldest = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
        for (int i = 0; i < 4; i++)
        {
            _timeProvider.Advance(TimeSpan.FromMinutes(1));
            await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
        }
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        await _accessRevoker.Received(1).RevokeSessionAsync(
            userId,
            oldest.Id.Value.ToString("N", CultureInfo.InvariantCulture),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSession_AtMaxSessions_EvictsOldest()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        // Create 5 sessions to reach max
        for (int i = 0; i < 5; i++)
        {
            await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
            // Advance time so sessions have distinct CreatedAt
            _timeProvider.Advance(TimeSpan.FromMinutes(1));
        }

        // 6th session should trigger eviction of the oldest
        ActiveSession session = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        session.Should().NotBeNull();
        await _messageBus.Received(1).PublishAsync(Arg.Any<UserSessionEvictedEvent>());
    }

    [Fact]
    public async Task CreateSession_Eviction_PublishesUserSessionEvictedEvent()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        // Fill to max
        for (int i = 0; i < 5; i++)
        {
            await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
            _timeProvider.Advance(TimeSpan.FromMinutes(1));
        }

        await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        await _messageBus.Received(1).PublishAsync(
            Arg.Is<UserSessionEvictedEvent>(e =>
                e.UserId == userId &&
                e.TenantId == tenantId &&
                e.Reason == "max_sessions_exceeded"));
    }

    // ──────────────────────────────────────────────
    // RevokeSessionAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RevokeSession_MarksTheLedgerRowRevoked()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ActiveSession session = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        await _sut.RevokeSessionAsync(session.Id.Value, userId, CancellationToken.None);

        ActiveSession? revoked = await _dbContext.ActiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == session.Id);
        revoked.Should().NotBeNull();
        revoked!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeSession_RevokesTheSessionsTokens()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ActiveSession session = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        await _sut.RevokeSessionAsync(session.Id.Value, userId, CancellationToken.None);

        await _accessRevoker.Received(1).RevokeSessionAsync(
            userId,
            session.Id.Value.ToString("N", CultureInfo.InvariantCulture),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeSession_NotFound_ThrowsInvalidOperation()
    {
        Guid userId = Guid.NewGuid();

        Func<Task> act = () => _sut.RevokeSessionAsync(Guid.NewGuid(), userId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────
    // GetActiveSessionsAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetActiveSessions_ReturnsOnlyNonRevokedNonExpired()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ActiveSession s1 = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
        ActiveSession s2 = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);
        ActiveSession s3 = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        // Revoke s1
        await _sut.RevokeSessionAsync(s1.Id.Value, userId, CancellationToken.None);

        List<ActiveSession> active = await _sut.GetActiveSessionsAsync(userId, CancellationToken.None);

        active.Should().HaveCount(2);
        active.Should().Contain(s => s.Id == s2.Id);
        active.Should().Contain(s => s.Id == s3.Id);
    }

    [Fact]
    public async Task GetActiveSessions_ExcludesExpiredSessions()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        // Advance past session duration (24h)
        _timeProvider.Advance(TimeSpan.FromHours(25));

        ActiveSession s2 = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        List<ActiveSession> active = await _sut.GetActiveSessionsAsync(userId, CancellationToken.None);

        active.Should().HaveCount(1);
        active.Should().Contain(s => s.Id == s2.Id);
    }
}
