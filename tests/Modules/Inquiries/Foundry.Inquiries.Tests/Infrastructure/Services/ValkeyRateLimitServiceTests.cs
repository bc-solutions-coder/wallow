using Foundry.Inquiries.Infrastructure.Services;
using StackExchange.Redis;

namespace Foundry.Inquiries.Tests.Infrastructure.Services;

public class ValkeyRateLimitServiceTests
{
    [Fact]
    public async Task IsAllowedAsync_FirstRequest_ReturnsTrue()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        IDatabase db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);

        ValkeyRateLimitService service = new(redis);

        bool result = await service.IsAllowedAsync("192.168.1.1", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_WhenCountIsAtLimit_ReturnsTrue()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        IDatabase db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(5L);

        ValkeyRateLimitService service = new(redis);

        bool result = await service.IsAllowedAsync("192.168.1.1", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAllowedAsync_WhenCountExceedsLimit_ReturnsFalse()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        IDatabase db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(6L);

        ValkeyRateLimitService service = new(redis);

        bool result = await service.IsAllowedAsync("192.168.1.1", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAllowedAsync_FirstRequest_SetsExpiry()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        IDatabase db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);

        ValkeyRateLimitService service = new(redis);

        await service.IsAllowedAsync("192.168.1.1", CancellationToken.None);

        await db.Received(1).KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IsAllowedAsync_SubsequentRequests_DoesNotResetExpiry()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        IDatabase db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(3L);

        ValkeyRateLimitService service = new(redis);

        await service.IsAllowedAsync("192.168.1.1", CancellationToken.None);

        await db.DidNotReceive().KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>(), Arg.Any<ExpireWhen>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task IsAllowedAsync_UsesIpAddressAsKey()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        IDatabase db = Substitute.For<IDatabase>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(db);
        db.StringIncrementAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>()).Returns(1L);

        ValkeyRateLimitService service = new(redis);
        string ipAddress = "203.0.113.5";

        await service.IsAllowedAsync(ipAddress, CancellationToken.None);

        await db.Received(1).StringIncrementAsync(
            Arg.Is<RedisKey>(k => k == ipAddress),
            Arg.Any<long>(),
            Arg.Any<CommandFlags>());
    }
}
