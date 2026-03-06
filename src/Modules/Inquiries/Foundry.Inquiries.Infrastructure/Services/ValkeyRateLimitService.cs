using Foundry.Inquiries.Application.Interfaces;
using StackExchange.Redis;

namespace Foundry.Inquiries.Infrastructure.Services;

public class ValkeyRateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;

    private const int MaxRequests = 5;
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(15);

    public ValkeyRateLimitService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default)
    {
        IDatabase db = _redis.GetDatabase();
        long count = await db.StringIncrementAsync(key);

        if (count == 1)
        {
            await db.KeyExpireAsync(key, _window);
        }

        return count <= MaxRequests;
    }
}
