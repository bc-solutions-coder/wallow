using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StackExchange.Redis;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;

namespace Wallow.Identity.Tests.Infrastructure;

#pragma warning disable CA2213 // Disposable fields should be disposed (NSubstitute mock)
public sealed class MfaLockoutServiceTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
#pragma warning disable CA2213 // IConnectionMultiplexer is a mock — no real resources to dispose
    private readonly IConnectionMultiplexer _mux;
#pragma warning restore CA2213
    private readonly IDatabase _redis;
    private readonly FakeTimeProvider _timeProvider;
    private readonly MfaLockoutService _sut;

    private const int MaxAttempts = 5;

    public MfaLockoutServiceTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: $"MfaLockout_{Guid.NewGuid()}")
            .Options;
        IDataProtectionProvider dp = DataProtectionProvider.Create("test");
        _dbContext = new IdentityDbContext(options, dp);

        _mux = Substitute.For<IConnectionMultiplexer>();
        _redis = Substitute.For<IDatabase>();
        _mux.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_redis);

        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        _sut = new MfaLockoutService(
            _dbContext,
            _mux,
            _timeProvider,
            NullLogger<MfaLockoutService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // ──────────────────────────────────────────────
    // UNIT 3: Atomic DB lockout persistence
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RecordFailure_BelowThreshold_ReturnsNotLockedOut()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        MfaLockoutResult result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);

        result.IsLockedOut.Should().BeFalse();
        result.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task RecordFailure_IncrementingBelowThreshold_TracksCount()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        MfaLockoutResult first = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        MfaLockoutResult second = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        MfaLockoutResult third = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);

        first.FailedAttempts.Should().Be(1);
        second.FailedAttempts.Should().Be(2);
        third.FailedAttempts.Should().Be(3);
        third.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task RecordFailure_AtThreshold_ReturnsLockedOut()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        MfaLockoutResult result = default!;
        for (int i = 0; i < MaxAttempts; i++)
        {
            result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }

        result.IsLockedOut.Should().BeTrue();
        result.FailedAttempts.Should().Be(MaxAttempts);
        result.LockoutEnd.Should().NotBeNull();
        result.LockoutEnd.Should().BeAfter(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task RecordFailure_AtThreshold_SetsLockoutCount()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        MfaLockoutResult result = default!;
        for (int i = 0; i < MaxAttempts; i++)
        {
            result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }

        result.LockoutCount.Should().Be(1);
    }

    [Fact]
    public async Task ResetAsync_ClearsAllFields()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        // Record some failures
        for (int i = 0; i < 3; i++)
        {
            await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }

        await _sut.ResetAsync(userId, CancellationToken.None);

        // Next failure should start fresh
        MfaLockoutResult result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        result.FailedAttempts.Should().Be(1);
        result.LockoutCount.Should().Be(0);
        result.IsLockedOut.Should().BeFalse();
        result.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public async Task RecordFailure_AfterReset_StartsFreshExponentialSeries()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        // First lockout cycle
        for (int i = 0; i < MaxAttempts; i++)
        {
            await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }
        await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);

        // Reset
        await _sut.ResetAsync(userId, CancellationToken.None);

        // Second lockout cycle — should start from attempt 1 again
        MfaLockoutResult afterReset = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        afterReset.FailedAttempts.Should().Be(1);
        afterReset.IsLockedOut.Should().BeFalse();
        afterReset.LockoutCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailure_SecondLockoutCycle_IncrementsLockoutCount()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        // First lockout
        for (int i = 0; i < MaxAttempts; i++)
        {
            await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }

        // Simulate lockout expiring by advancing time, then fail again to second lockout
        _timeProvider.Advance(TimeSpan.FromHours(2));

        // Reset attempts but keep lockout count (simulating auto-unlock after expiry)
        // Then accumulate failures again to second lockout
        for (int i = 0; i < MaxAttempts; i++)
        {
            await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }

        MfaLockoutResult result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        result.LockoutCount.Should().BeGreaterThanOrEqualTo(2);
    }

    // ──────────────────────────────────────────────
    // UNIT 4: Redis cache layer
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RecordFailure_CacheHit_ShortCircuitsDbCall()
    {
        Guid userId = Guid.NewGuid();
        DateTimeOffset lockoutEnd = _timeProvider.GetUtcNow().AddMinutes(30);

        // Setup Redis to return a cached lockout
        SetupRedisCacheHit(userId, lockoutEnd);

        MfaLockoutResult result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);

        result.IsLockedOut.Should().BeTrue();
        result.LockoutEnd.Should().NotBeNull();
    }

    [Fact]
    public async Task RecordFailure_CacheMiss_ProceedsToDb()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        MfaLockoutResult result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);

        // Should have proceeded to DB and returned a result with attempt count
        result.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task RecordFailure_DbProducedLockout_WritesRedisKeyWithTtl()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        // Drive to lockout threshold
        for (int i = 0; i < MaxAttempts; i++)
        {
            await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);
        }

        // Verify Redis SET was called with an expiry for the lockout
        await _redis.Received().StringSetAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())),
            Arg.Any<RedisValue>(),
            Arg.Is<TimeSpan?>(ts => ts.HasValue && ts.Value.TotalSeconds > 0),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task ResetAsync_DeletesRedisKey()
    {
        Guid userId = Guid.NewGuid();
        SetupRedisCacheMiss();

        await _sut.ResetAsync(userId, CancellationToken.None);

        await _redis.Received().KeyDeleteAsync(
            Arg.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task RecordFailure_RedisError_DoesNotCrash_FallsThroughToDb()
    {
        Guid userId = Guid.NewGuid();

        // Setup Redis to throw on every operation
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<RedisValue>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));
        _redis.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
            .Returns<bool>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        // Should not throw — falls through to DB
        MfaLockoutResult result = await _sut.RecordFailureAsync(userId, MaxAttempts, CancellationToken.None);

        result.FailedAttempts.Should().Be(1);
        result.IsLockedOut.Should().BeFalse();
    }

    [Fact]
    public async Task ResetAsync_RedisError_DoesNotCrash()
    {
        Guid userId = Guid.NewGuid();

        _redis.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<bool>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        // Should not throw
        Func<Task> act = () => _sut.ResetAsync(userId, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private void SetupRedisCacheMiss()
    {
        _redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);
    }

    private void SetupRedisCacheHit(Guid userId, DateTimeOffset lockoutEnd)
    {
        _redis.StringGetAsync(
                Arg.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())),
                Arg.Any<CommandFlags>())
            .Returns(new RedisValue(lockoutEnd.ToUnixTimeSeconds().ToString()));
    }
}
