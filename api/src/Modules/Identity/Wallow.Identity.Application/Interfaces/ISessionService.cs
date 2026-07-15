using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface ISessionService
{
    Task<ActiveSession> CreateSessionAsync(Guid userId, Guid tenantId, CancellationToken ct);
    Task RevokeSessionAsync(Guid sessionId, Guid userId, CancellationToken ct);
    Task<List<ActiveSession>> GetActiveSessionsAsync(Guid userId, CancellationToken ct);
    Task TouchSessionAsync(string sessionToken, CancellationToken ct);
}
