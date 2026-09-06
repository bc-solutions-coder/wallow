using NSubstitute;
using StackExchange.Redis;
using Wallow.Shared.Infrastructure.RateLimiting;

namespace Wallow.Shared.Infrastructure.Tests.RateLimiting;

public class RedisFixedWindowCounterTests
{
    [Fact]
    public async Task FirstAttempt_StartsTheWindow()
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.StringIncrementAsync("first", 1, CommandFlags.None).Returns(1L);
        RedisFixedWindowCounter counter = new(redis);

        (await counter.IncrementAsync("first", TimeSpan.FromMinutes(15))).Should().Be(1);

        await database.Received().KeyExpireAsync("first", TimeSpan.FromMinutes(15), ExpireWhen.Always, CommandFlags.None);
    }

    [Fact]
    public async Task LaterAttempts_KeepTheOriginalWindow_AndKeysAreIndependent()
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.StringIncrementAsync("first", 1, CommandFlags.None).Returns(2L, 3L);
        database.StringIncrementAsync("second", 1, CommandFlags.None).Returns(1L);
        RedisFixedWindowCounter counter = new(redis);

        (await counter.IncrementAsync("first", TimeSpan.FromMinutes(15))).Should().Be(2);
        (await counter.IncrementAsync("second", TimeSpan.FromHours(1))).Should().Be(1);
        (await counter.IncrementAsync("first", TimeSpan.FromMinutes(15))).Should().Be(3);

        await database.DidNotReceive().KeyExpireAsync("first", Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>());
        await database.Received().KeyExpireAsync("second", TimeSpan.FromHours(1), ExpireWhen.Always, CommandFlags.None);
    }

    [Theory]
    [InlineData(42, 42)]
    [InlineData(null, 900)]
    [InlineData(0, 900)]
    [InlineData(-1, 900)]
    public async Task RetryDelay_UsesPositiveTtl_OrTheWindow(int? ttlSeconds, int expectedSeconds)
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.KeyTimeToLiveAsync("first").Returns(ttlSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
        RedisFixedWindowCounter counter = new(redis);

        TimeSpan retryAfter = await counter.GetRetryAfterAsync("first", TimeSpan.FromMinutes(15));

        retryAfter.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }
}
