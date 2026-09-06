using StackExchange.Redis;
using Wallow.Shared.Contracts.RateLimiting;

namespace Wallow.Shared.Infrastructure.RateLimiting;

public sealed class RedisFixedWindowCounter(IConnectionMultiplexer redis) : IFixedWindowCounter
{
    public async Task<long> IncrementAsync(string key, TimeSpan window)
    {
        IDatabase database = redis.GetDatabase();
        long count = await database.StringIncrementAsync(key);
        if (count == 1)
        {
            await database.KeyExpireAsync(key, window);
        }

        return count;
    }

    public async Task<TimeSpan> GetRetryAfterAsync(string key, TimeSpan window)
    {
        TimeSpan? remaining = await redis.GetDatabase().KeyTimeToLiveAsync(key);
        return remaining is { } wait && wait > TimeSpan.Zero ? wait : window;
    }
}
