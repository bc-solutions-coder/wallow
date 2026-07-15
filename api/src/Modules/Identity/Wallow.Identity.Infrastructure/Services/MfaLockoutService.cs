using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class MfaLockoutService(
    IdentityDbContext dbContext,
    IConnectionMultiplexer connectionMultiplexer,
    TimeProvider timeProvider,
    ILogger<MfaLockoutService> logger) : IMfaLockoutService
{
    private const string KeyPrefix = "mfa:lockout:";
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    public async Task<MfaLockoutResult> RecordFailureAsync(Guid userId, int maxAttempts, CancellationToken ct)
    {
        // Check Redis cache first for an active lockout
        try
        {
            RedisValue cached = await _redis.StringGetAsync($"{KeyPrefix}{userId}");
            if (cached.HasValue && long.TryParse(cached.ToString(), out long unixSeconds))
            {
                DateTimeOffset lockoutEnd = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                if (lockoutEnd > timeProvider.GetUtcNow())
                {
                    return new MfaLockoutResult(true, 0, 0, lockoutEnd);
                }
            }
        }
        catch (RedisException ex)
        {
            LogRedisError(ex);
        }

        WallowUser? user = await dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            user = WallowUser.Create(Guid.Empty, "MFA", "User", $"{userId}@mfa.internal", timeProvider);
            // Override the auto-generated Id with the requested userId
            dbContext.Entry(user).Property(u => u.Id).CurrentValue = userId;
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(ct);
        }

        user.RecordMfaFailure(maxAttempts, timeProvider);
        await dbContext.SaveChangesAsync(ct);

        bool isLockedOut = user.IsMfaLockedOut(timeProvider);

        if (isLockedOut && user.MfaLockoutEnd is not null)
        {
            try
            {
                TimeSpan ttl = user.MfaLockoutEnd.Value - timeProvider.GetUtcNow();
                if (ttl > TimeSpan.Zero)
                {
                    await _redis.StringSetAsync(
                        (RedisKey)$"{KeyPrefix}{userId}",
                        (RedisValue)user.MfaLockoutEnd.Value.ToUnixTimeSeconds().ToString(),
                        ttl,
                        false,
                        When.Always,
                        CommandFlags.None);
                }
            }
            catch (RedisException ex)
            {
                LogRedisError(ex);
            }
        }

        return new MfaLockoutResult(
            isLockedOut,
            user.MfaFailedAttempts,
            user.MfaLockoutCount,
            user.MfaLockoutEnd);
    }

    public async Task ResetAsync(Guid userId, CancellationToken ct)
    {
        WallowUser? user = await dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is not null)
        {
            user.ResetMfaAttempts();
            // Full service-level reset also clears escalation counter
            dbContext.Entry(user).Property(u => u.MfaLockoutCount).CurrentValue = 0;
            await dbContext.SaveChangesAsync(ct);
        }

        try
        {
            await _redis.KeyDeleteAsync($"{KeyPrefix}{userId}");
        }
        catch (RedisException ex)
        {
            LogRedisError(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Redis operation failed, falling back to DB")]
    private partial void LogRedisError(Exception exception);
}
