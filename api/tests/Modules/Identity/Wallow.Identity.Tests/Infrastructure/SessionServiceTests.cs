using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StackExchange.Redis;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

#pragma warning disable CA2213 // Disposable fields should be disposed (NSubstitute mock)
public sealed class SessionServiceTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
#pragma warning disable CA2213 // IConnectionMultiplexer is a mock — no real resources to dispose
    private readonly IConnectionMultiplexer _mux;
#pragma warning restore CA2213
    private readonly IDatabase _redis;
    private readonly IMessageBus _messageBus;
    private readonly FakeTimeProvider _timeProvider;
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: $"Session_{Guid.NewGuid()}")
            .Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dp);

        _mux = Substitute.For<IConnectionMultiplexer>();
        _redis = Substitute.For<IDatabase>();
        _mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redis);
        _mux.GetDatabase().Returns(_redis);

        _messageBus = Substitute.For<IMessageBus>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _sut = new SessionService(
            _dbContext,
            _mux,
            _messageBus,
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

    [Fact]
    public async Task CreateSession_Eviction_SetsRedisRevokedKey()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        // Fill to max
        List<ActiveSession> sessions = [];
        for (int i = 0; i < 5; i++)
        {
            sessions.Add(await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None));
            _timeProvider.Advance(TimeSpan.FromMinutes(1));
        }

        string oldestToken = sessions[0].SessionToken;

        await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        await _redis.Received(1).StringSetAsync(
            $"session:revoked:{oldestToken}",
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(ts => ts != null && ts.Value.TotalHours == 24),
            Arg.Any<When>());
    }

    // ──────────────────────────────────────────────
    // RevokeSessionAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RevokeSession_SetsIsRevokedAndRedisKey()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        ActiveSession session = await _sut.CreateSessionAsync(userId, tenantId, CancellationToken.None);

        await _sut.RevokeSessionAsync(session.Id.Value, userId, CancellationToken.None);

        // Verify session is revoked in DB
        ActiveSession? revoked = await _dbContext.ActiveSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == session.Id);
        revoked.Should().NotBeNull();
        revoked!.IsRevoked.Should().BeTrue();

        // Verify Redis key was set
        await _redis.Received().StringSetAsync(
            $"session:revoked:{session.SessionToken}",
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(ts => ts != null && ts.Value.TotalHours == 24),
            Arg.Any<When>());
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
