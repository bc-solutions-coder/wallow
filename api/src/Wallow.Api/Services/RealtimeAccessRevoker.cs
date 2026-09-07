using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

/// <summary>
/// Closes matching SSE and hub connections registered in this host process,
/// by user and tenant or by client id.
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
