using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Infrastructure.RateLimiting;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Infrastructure;

public class EmailChangeRateLimiterTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public async Task Check_UsesTheConfiguredCap(int count, bool allowed)
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.StringIncrementAsync("email:change:rate:user", 1, CommandFlags.None).Returns(count);
        EmailChangeRateLimiter limiter = new(new RedisFixedWindowCounter(redis),
            Options.Create(new EmailChangeOptions { RateLimitMaxRequests = 2, RateLimitWindow = TimeSpan.FromHours(2) }));

        Result result = await limiter.CheckAsync("user");

        result.IsSuccess.Should().Be(allowed);
        if (!allowed)
        {
            result.Error.Code.Should().Be("RateLimit.Exceeded");
            result.Error.RetryAfter.Should().Be(TimeSpan.FromHours(2));
        }
    }

    [Theory]
    [InlineData(38, 38)]
    [InlineData(null, 3600)]
    [InlineData(0, 3600)]
    [InlineData(-1, 3600)]
    public async Task DefaultLimit_RejectsFourthAttempt_WithRemainingDelay(int? ttlSeconds, int expectedSeconds)
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.StringIncrementAsync("email:change:rate:user", 1, CommandFlags.None).Returns(4L);
        database.KeyTimeToLiveAsync("email:change:rate:user").Returns(ttlSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null);
        EmailChangeRateLimiter limiter = new(new RedisFixedWindowCounter(redis), Options.Create(new EmailChangeOptions()));

        Result result = await limiter.CheckAsync("user");

        result.Error.Code.Should().Be("RateLimit.Exceeded");
        result.Error.RetryAfter.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public async Task DefaultLimit_AllowsThirdAttempt()
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.StringIncrementAsync("email:change:rate:user", 1, CommandFlags.None).Returns(3L);
        EmailChangeRateLimiter limiter = new(new RedisFixedWindowCounter(redis), Options.Create(new EmailChangeOptions()));

        (await limiter.CheckAsync("user")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FirstAttempt_UsesTheConfiguredWindow()
    {
        IDatabase database = Substitute.For<IDatabase>();
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().ReturnsForAnyArgs(database);
        database.StringIncrementAsync("email:change:rate:user", 1, CommandFlags.None).Returns(1L);
        EmailChangeRateLimiter limiter = new(new RedisFixedWindowCounter(redis),
            Options.Create(new EmailChangeOptions { RateLimitWindow = TimeSpan.FromHours(2) }));

        (await limiter.CheckAsync("user")).IsSuccess.Should().BeTrue();

        await database.Received().KeyExpireAsync("email:change:rate:user", TimeSpan.FromHours(2), ExpireWhen.Always, CommandFlags.None);
    }
}
