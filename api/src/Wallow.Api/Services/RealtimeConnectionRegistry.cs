using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Wallow.Api.Services;

/// <summary>
/// Tracks local hub caller contexts so revocation can abort existing connections.
/// </summary>
public class RealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredConnection> _connections = new();

    public virtual void Register(
        string connectionId, string userId, Guid tenantId, HubCallerContext context, string? clientId = null)
    {
        _connections[connectionId] = new RegisteredConnection(userId, tenantId, clientId, context);
    }

    public virtual void Unregister(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Aborts registered connections for this user and tenant; removing groups alone would allow rejoining.
    /// </summary>
    public virtual void AbortConnectionsForUser(string userId, Guid tenantId)
    {
        AbortConnectionsWhere(connection => connection.UserId == userId && connection.TenantId == tenantId);
    }

    /// <summary>
    /// Aborts registered local connections associated with this client.
    /// </summary>
    public virtual void AbortConnectionsForClient(string clientId)
    {
        AbortConnectionsWhere(connection => connection.ClientId == clientId);
    }

    private void AbortConnectionsWhere(Func<RegisteredConnection, bool> matches)
    {
        foreach (KeyValuePair<string, RegisteredConnection> entry in _connections)
        {
            if (!matches(entry.Value))
            {
                continue;
            }

            _connections.TryRemove(entry.Key, out _);
            entry.Value.Context.Abort();
        }
    }

    private sealed record RegisteredConnection(string UserId, Guid TenantId, string? ClientId, HubCallerContext Context);
}
