using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Wallow.Api.Services;

/// <summary>
/// The open hub connections, keyed by connection id.
///
/// SignalR offers no way to end a connection from outside the hub: <c>IHubContext</c> can address
/// groups and clients but not close them, and only <see cref="HubCallerContext.Abort"/> hangs up.
/// The hub therefore lends its caller context here for the lifetime of the connection, which is
/// what lets a revocation reach a socket opened before it.
/// </summary>
public class RealtimeConnectionRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredConnection> _connections = new();

    public virtual void Register(string connectionId, string userId, Guid tenantId, HubCallerContext context)
    {
        _connections[connectionId] = new RegisteredConnection(userId, tenantId, context);
    }

    public virtual void Unregister(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    /// <summary>
    /// Hangs up every connection this person holds in this tenant. Aborting rather than removing
    /// them from their groups is deliberate: <c>JoinGroup</c> would let an aborted-in-name-only
    /// client walk straight back into the tenant group it was just taken out of.
    /// </summary>
    public virtual void AbortConnectionsForUser(string userId, Guid tenantId)
    {
        foreach (KeyValuePair<string, RegisteredConnection> entry in _connections)
        {
            if (entry.Value.UserId != userId || entry.Value.TenantId != tenantId)
            {
                continue;
            }

            _connections.TryRemove(entry.Key, out _);
            entry.Value.Context.Abort();
        }
    }

    private sealed record RegisteredConnection(string UserId, Guid TenantId, HubCallerContext Context);
}
