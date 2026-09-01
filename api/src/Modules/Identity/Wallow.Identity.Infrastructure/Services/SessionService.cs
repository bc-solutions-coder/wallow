using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class SessionService(
    IdentityDbContext dbContext,
    IMessageBus messageBus,
    IAccessRevoker accessRevoker,
    TimeProvider timeProvider,
    ILogger<SessionService> logger) : ISessionService
{
    private const int MaxSessions = 5;
    private static readonly TimeSpan _sessionDuration = TimeSpan.FromHours(24);

    public async Task<ActiveSession> CreateSessionAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        // In production, pg_advisory_xact_lock(userId.GetHashCode()) would be called here
        // to prevent race conditions. Skipped for in-memory/unit test compatibility.
        await AcquireAdvisoryLockAsync(userId, ct);

        DateTimeOffset now = timeProvider.GetUtcNow();

        List<ActiveSession> activeSessions = await dbContext.ActiveSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now)
            .OrderBy(s => s.CreatedAt)
            .AsTracking()
            .ToListAsync(ct);

        if (activeSessions.Count >= MaxSessions)
        {
            ActiveSession oldest = activeSessions[0];
            oldest.Revoke();

            // The ledger row id doubles as the OIDC sid (see ActiveSession), so evicting the
            // row must also kill the tokens minted under that sid.
            await accessRevoker.RevokeSessionAsync(userId, oldest.Sid, ct);

            await messageBus.PublishAsync(new UserSessionEvictedEvent
            {
                UserId = userId,
                TenantId = tenantId,
                SessionId = oldest.Id.Value,
                Reason = "max_sessions_exceeded"
            });

            LogSessionEvicted(oldest.Id.Value, userId);
        }

        ActiveSession session = ActiveSession.Create(userId, tenantId, _sessionDuration, timeProvider);
        dbContext.ActiveSessions.Add(session);
        await dbContext.SaveChangesAsync(ct);

        LogSessionCreated(session.Id.Value, userId);
        return session;
    }

    public async Task RevokeSessionAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        ActiveSessionId id = ActiveSessionId.Create(sessionId);
        ActiveSession session = await dbContext.ActiveSessions
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found for user {userId}.");

        session.Revoke();

        // Tokens first, ledger second — same fail-closed order as eviction: if the save fails
        // the row still looks active, but the tokens under its sid are already dead.
        await accessRevoker.RevokeSessionAsync(userId, session.Sid, ct);

        await dbContext.SaveChangesAsync(ct);

        LogSessionRevoked(sessionId, userId);
    }

    public Task<List<ActiveSession>> GetActiveSessionsAsync(Guid userId, CancellationToken ct)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        return dbContext.ActiveSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task TouchSessionAsync(string sessionToken, CancellationToken ct)
    {
        ActiveSession? session = await dbContext.ActiveSessions
            .AsTracking()
            .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && !s.IsRevoked, ct);

        if (session is null)
        {
            return;
        }

        session.Touch(timeProvider);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task AcquireAdvisoryLockAsync(Guid userId, CancellationToken ct)
    {
        // pg_advisory_xact_lock ensures only one session creation per user at a time.
        // This is a no-op when using InMemory provider (unit tests).
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})", [userId.GetHashCode()], ct);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} created for user {UserId}")]
    private partial void LogSessionCreated(Guid sessionId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} evicted for user {UserId}")]
    private partial void LogSessionEvicted(Guid sessionId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} revoked for user {UserId}")]
    private partial void LogSessionRevoked(Guid sessionId, Guid userId);
}
