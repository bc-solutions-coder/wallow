using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Infrastructure.RateLimiting;

namespace Wallow.Identity.Tests.Infrastructure;

public class InvalidClientLockoutTests
{
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly ILogger<InvalidClientLockout> _logger = Substitute.For<ILogger<InvalidClientLockout>>();
    private readonly InvalidClientLockout _lockout;

    public InvalidClientLockoutTests()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(_database);
        _logger.IsEnabled(LogLevel.Warning).Returns(true);
        _lockout = new InvalidClientLockout(redis, new RedisFixedWindowCounter(redis),
            Options.Create(new InvalidClientLockoutOptions { FailureThreshold = 3, WindowMinutes = 2, LockoutMinutes = 7 }), _logger);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public async Task FailedAuthentication_LocksAtTheThreshold(long count, bool expectedLockout)
    {
        _database.StringIncrementAsync("identity:invalid-client:failures:client", 1, CommandFlags.None).Returns(count);

        await _lockout.RecordFailureAsync("client", CancellationToken.None);

        if (expectedLockout)
        {
            await _database.Received().StringSetAsync("identity:invalid-client:lockout:client", "1", TimeSpan.FromMinutes(7));
        }
        else
        {
            await _database.DidNotReceiveWithAnyArgs().StringSetAsync("unused", "1", TimeSpan.FromMinutes(7));
        }

        if (count == 1)
        {
            await _database.Received().KeyExpireAsync("identity:invalid-client:failures:client", TimeSpan.FromMinutes(2), ExpireWhen.Always, CommandFlags.None);
        }
    }

    [Fact]
    public async Task FurtherFailures_RenewTheLockoutWithoutRefreshingTheCounter()
    {
        _database.StringIncrementAsync("identity:invalid-client:failures:client", 1, CommandFlags.None).Returns(3L, 4L);

        await _lockout.RecordFailureAsync("client", CancellationToken.None);
        await _lockout.RecordFailureAsync("client", CancellationToken.None);

        await _database.Received(2).StringSetAsync("identity:invalid-client:lockout:client", "1", TimeSpan.FromMinutes(7));
        await _database.DidNotReceiveWithAnyArgs().KeyExpireAsync(default, default(TimeSpan?), default, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lookup_UsesTheSeparateLockoutKey(bool locked)
    {
        _database.KeyExistsAsync("identity:invalid-client:lockout:client").Returns(locked);

        (await _lockout.IsLockedOutAsync("client", CancellationToken.None)).Should().Be(locked);
    }

    [Fact]
    public async Task RedisFailure_IsLogged_AndDoesNotBlockAuthentication()
    {
        _database.StringIncrementAsync(Arg.Any<RedisKey>()).ThrowsAsync(new RedisException("unavailable"));
        _database.KeyExistsAsync(Arg.Any<RedisKey>()).ThrowsAsync(new RedisException("unavailable"));

        await _lockout.RecordFailureAsync("client", CancellationToken.None);
        bool locked = await _lockout.IsLockedOutAsync("client", CancellationToken.None);

        locked.Should().BeFalse();
        _logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "Log").Should().Be(2);
    }

    [Fact]
    public async Task LockoutWriteFailure_IsLoggedAndTolerated()
    {
        _database.StringIncrementAsync("identity:invalid-client:failures:client", 1, CommandFlags.None).Returns(3L);
        _database.StringSetAsync("identity:invalid-client:lockout:client", "1", TimeSpan.FromMinutes(7))
            .ThrowsAsync(new RedisException("unavailable"));

        await _lockout.RecordFailureAsync("client", CancellationToken.None);

        _logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "Log").Should().Be(1);
    }
}
