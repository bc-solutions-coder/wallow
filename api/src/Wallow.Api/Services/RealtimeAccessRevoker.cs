using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

/// <summary>
/// Closes both kinds of realtime connection this host serves — the SSE streams and the hub
/// sockets — for one person in one tenant, or for every holder of one client's tokens.
/// </summary>
public sealed partial class RealtimeAccessRevoker(
    SseConnectionManager sseConnections,
    RealtimeConnectionRegistry hubConnections,
    ILogger<RealtimeAccessRevoker> logger) : IRealtimeAccessRevoker
{
    public Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        sseConnections.CloseConnectionsForUser(userId, tenantId);
        hubConnections.AbortConnectionsForUser(userId, tenantId);

        LogRealtimeAccessRevoked(userId, tenantId);

        return Task.CompletedTask;
    }

    public Task RevokeClientAsync(string clientId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        sseConnections.CloseConnectionsForClient(clientId);
        hubConnections.AbortConnectionsForClient(clientId);

        LogClientRealtimeAccessRevoked(clientId);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Realtime connections closed for user {UserId} in tenant {TenantId}")]
    private partial void LogRealtimeAccessRevoked(string userId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Realtime connections closed for client {ClientId}")]
    private partial void LogClientRealtimeAccessRevoked(string clientId);
}
