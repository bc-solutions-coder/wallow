using StackExchange.Redis;

namespace Wallow.ApiKeys.Infrastructure.Services;

public sealed class RedisDatabaseWrapper(IDatabase db) : IRedisDatabase
{
    public Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)
        => db.StringSetAsync(key, value, expiry, keepTtl, when, flags);

    public Task<RedisValue> StringGetAsync(RedisKey key)
        => db.StringGetAsync(key);

    public Task<bool> SetAddAsync(RedisKey key, RedisValue value)
        => db.SetAddAsync(key, value);

    public Task<long> SetLengthAsync(RedisKey key)
        => db.SetLengthAsync(key);

    public Task<bool> KeyDeleteAsync(RedisKey key)
        => db.KeyDeleteAsync(key);

    public Task<bool> SetRemoveAsync(RedisKey key, RedisValue value)
        => db.SetRemoveAsync(key, value);
}
