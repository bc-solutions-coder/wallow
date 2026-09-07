using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Shared.Contracts.RateLimiting;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Uses shared Redis state to count failures across API instances. Redis exceptions
/// fail open so a counter-store outage does not reject every client authentication.
/// </summary>
public sealed partial class InvalidClientLockout(
    IConnectionMultiplexer connectionMultiplexer,
    IFixedWindowCounter counter,
    IOptions<InvalidClientLockoutOptions> options,
    ILogger<InvalidClientLockout> logger) : IInvalidClientLockout
{
    private const string FailuresKeyPrefix = "identity:invalid-client:failures:";
    private const string LockoutKeyPrefix = "identity:invalid-client:lockout:";

    public async Task RecordFailureAsync(string clientId, CancellationToken ct)
    {
        InvalidClientLockoutOptions lockout = options.Value;
        try
        {
            IDatabase redis = connectionMultiplexer.GetDatabase();
            string failuresKey = $"{FailuresKeyPrefix}{clientId}";
            long failures = await counter.IncrementAsync(failuresKey, TimeSpan.FromMinutes(lockout.WindowMinutes));

            if (failures >= lockout.FailureThreshold)
            {
                await redis.StringSetAsync(
                    $"{LockoutKeyPrefix}{clientId}",
                    "1",
                    TimeSpan.FromMinutes(lockout.LockoutMinutes));
            }
        }
        catch (RedisException ex)
        {
            LogRedisError(ex);
        }
    }

    public async Task<bool> IsLockedOutAsync(string clientId, CancellationToken ct)
    {
        try
        {
            return await connectionMultiplexer.GetDatabase()
                .KeyExistsAsync($"{LockoutKeyPrefix}{clientId}");
        }
        catch (RedisException ex)
        {
            LogRedisError(ex);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis operation failed; invalid_client lockout is inactive for this call")]
    private partial void LogRedisError(Exception exception);
}
