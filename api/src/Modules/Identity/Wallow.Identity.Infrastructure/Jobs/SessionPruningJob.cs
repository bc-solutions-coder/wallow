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
}
