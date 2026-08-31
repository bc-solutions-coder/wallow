using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// The counter lives in Redis so every API instance sees the same tally: a guesser spreading
/// attempts across replicas earns one lockout, not one per replica. The brake fails open on a
/// Redis outage — client authentication itself still stands, and refusing every client because
/// the counter store hiccuped would turn the guard into an outage of its own.
/// </summary>
public sealed partial class InvalidClientLockout(
    IConnectionMultiplexer connectionMultiplexer,
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
            RedisKey failuresKey = $"{FailuresKeyPrefix}{clientId}";
            long failures = await redis.StringIncrementAsync(failuresKey);
            if (failures == 1)
            {
                await redis.KeyExpireAsync(failuresKey, TimeSpan.FromMinutes(lockout.WindowMinutes));
            }

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
