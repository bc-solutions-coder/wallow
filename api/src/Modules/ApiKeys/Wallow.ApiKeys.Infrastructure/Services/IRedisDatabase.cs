using StackExchange.Redis;

namespace Wallow.ApiKeys.Infrastructure.Services;

public interface IRedisDatabase
{
    Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags);
    Task<RedisValue> StringGetAsync(RedisKey key);
    Task<bool> SetAddAsync(RedisKey key, RedisValue value);
    Task<long> SetLengthAsync(RedisKey key);
    Task<bool> KeyDeleteAsync(RedisKey key);
    Task<bool> SetRemoveAsync(RedisKey key, RedisValue value);
}
