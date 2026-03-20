using Wallow.Inquiries.Application.Interfaces;
using StackExchange.Redis;

namespace Wallow.Inquiries.Infrastructure.Services;

public class ValkeyRateLimitService(IConnectionMultiplexer redis) : IRateLimitService
{
    private const int MaxRequests = 5;
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(15);

    public async Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default)
    {
        IDatabase db = redis.GetDatabase();
        long count = await db.StringIncrementAsync(key);

        if (count == 1)
        {
            await db.KeyExpireAsync(key, _window);
        }

        return count <= MaxRequests;
    }
}
