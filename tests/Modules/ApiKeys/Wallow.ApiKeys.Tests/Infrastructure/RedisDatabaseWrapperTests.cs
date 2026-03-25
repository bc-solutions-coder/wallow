using StackExchange.Redis;
using Wallow.ApiKeys.Infrastructure.Services;

namespace Wallow.ApiKeys.Tests.Infrastructure;

public class RedisDatabaseWrapperTests
{
    private readonly IDatabase _innerDb;
    private readonly RedisDatabaseWrapper _sut;

    public RedisDatabaseWrapperTests()
    {
        _innerDb = Substitute.For<IDatabase>();
        _sut = new RedisDatabaseWrapper(_innerDb);
    }

    [Fact]
    public async Task StringSetAsync_DelegatesToInnerDatabase()
    {
        RedisKey key = "test-key";
        RedisValue value = "test-value";
        TimeSpan? expiry = TimeSpan.FromMinutes(5);
        _innerDb.StringSetAsync(key, value, expiry, false, When.Always, CommandFlags.None)
            .Returns(true);

        bool result = await _sut.StringSetAsync(key, value, expiry, false, When.Always, CommandFlags.None);

        result.Should().BeTrue();
        await _innerDb.Received(1).StringSetAsync(key, value, expiry, false, When.Always, CommandFlags.None);
    }

    [Fact]
    public async Task StringGetAsync_DelegatesToInnerDatabase()
    {
        RedisKey key = "test-key";
        _innerDb.StringGetAsync(key).Returns((RedisValue)"stored-value");

        RedisValue result = await _sut.StringGetAsync(key);

        result.Should().Be((RedisValue)"stored-value");
        await _innerDb.Received(1).StringGetAsync(key);
    }

    [Fact]
    public async Task SetAddAsync_DelegatesToInnerDatabase()
    {
        RedisKey key = "test-set";
        RedisValue value = "member";
        _innerDb.SetAddAsync(key, value).Returns(true);

        bool result = await _sut.SetAddAsync(key, value);

        result.Should().BeTrue();
        await _innerDb.Received(1).SetAddAsync(key, value);
    }

    [Fact]
    public async Task SetLengthAsync_DelegatesToInnerDatabase()
    {
        RedisKey key = "test-set";
        _innerDb.SetLengthAsync(key).Returns(42L);

        long result = await _sut.SetLengthAsync(key);

        result.Should().Be(42L);
        await _innerDb.Received(1).SetLengthAsync(key);
    }

    [Fact]
    public async Task KeyDeleteAsync_DelegatesToInnerDatabase()
    {
        RedisKey key = "test-key";
        _innerDb.KeyDeleteAsync(key).Returns(true);

        bool result = await _sut.KeyDeleteAsync(key);

        result.Should().BeTrue();
        await _innerDb.Received(1).KeyDeleteAsync(key);
    }

    [Fact]
    public async Task SetRemoveAsync_DelegatesToInnerDatabase()
    {
        RedisKey key = "test-set";
        RedisValue value = "member-to-remove";
        _innerDb.SetRemoveAsync(key, value).Returns(true);

        bool result = await _sut.SetRemoveAsync(key, value);

        result.Should().BeTrue();
        await _innerDb.Received(1).SetRemoveAsync(key, value);
    }
}
