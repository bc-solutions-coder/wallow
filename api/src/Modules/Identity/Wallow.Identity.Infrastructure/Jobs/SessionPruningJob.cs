using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Jobs;

public sealed partial class SessionPruningJob(
    IdentityDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SessionPruningJob> logger)
{
    /// <summary>
    /// Age past which an SSO-session participation row is assumed abandoned. Rows are normally
    /// deleted at logout; this backstop only catches sessions whose identity cookie expired
    /// without one, so it just needs to sit safely beyond the cookie's sliding lifetime.
    /// </summary>
    private static readonly TimeSpan _ssoParticipationMaxAge = TimeSpan.FromDays(30);

    public async Task<int> ExecuteAsync()
    {
        LogPruningStarted(logger);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();

            List<ActiveSession> staleSessions = await dbContext.ActiveSessions
                .AsTracking()
                .Where(s => s.IsRevoked || s.ExpiresAt < now)
                .ToListAsync();

            if (staleSessions.Count > 0)
            {
                dbContext.ActiveSessions.RemoveRange(staleSessions);
                await dbContext.SaveChangesAsync();
            }

            DateTimeOffset participationCutoff = now - _ssoParticipationMaxAge;
            List<SsoSessionClient> staleParticipations = await dbContext.SsoSessionClients
                .AsTracking()
                .Where(s => s.CreatedAt < participationCutoff)
                .ToListAsync();

            if (staleParticipations.Count > 0)
            {
                dbContext.SsoSessionClients.RemoveRange(staleParticipations);
                await dbContext.SaveChangesAsync();
                LogSsoParticipationPruned(logger, staleParticipations.Count);
            }

            LogPruningCompleted(logger, staleSessions.Count);

            return staleSessions.Count;
        }
        catch (Exception ex)
        {
            LogPruningFailed(logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting session pruning")]
    private static partial void LogPruningStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session pruning completed, removed {Count} sessions")]
    private static partial void LogPruningCompleted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Session pruning failed")]
    private static partial void LogPruningFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed {Count} abandoned SSO-session participation rows")]
    private static partial void LogSsoParticipationPruned(ILogger logger, int count);
}
