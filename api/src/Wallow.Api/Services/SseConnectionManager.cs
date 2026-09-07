using System.Collections.Concurrent;
using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

public class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseConnectionState> _connections = new();

    // The manager owns cancellation sources separately from dispatch state.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    public virtual void AddConnection(
        string connectionId,
        string userId,
        Guid tenantId,
        HashSet<string> modules,
        HashSet<string> permissions,
        HashSet<string> roles,
        string? clientId = null)
    {
        Channel<RealtimeEnvelope> channel = Channel.CreateBounded<RealtimeEnvelope>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        SseConnectionState state = new(userId, tenantId, modules, permissions, roles, channel, clientId);
        _connections[connectionId] = state;

        // RemoveConnection disposes the stored source.
#pragma warning disable CA2000
        _cancellations[connectionId] = new CancellationTokenSource();
#pragma warning restore CA2000
    }

    public virtual void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);

        if (_cancellations.TryRemove(connectionId, out CancellationTokenSource? cancellation))
        {
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// Returns the stream cancellation token, or None for an unknown connection.
    /// Cancellation ends the endpoint heartbeat loop as well as deliveries.
    /// </summary>
    public virtual CancellationToken GetCancellationToken(string connectionId)
    {
        return _cancellations.TryGetValue(connectionId, out CancellationTokenSource? cancellation)
            ? cancellation.Token
            : CancellationToken.None;
    }

    /// <summary>
    /// Cancels registered local streams for the user and tenant.
    /// </summary>
    public virtual void CloseConnectionsForUser(string userId, Guid tenantId)
    {
        CloseConnectionsWhere(state => state.UserId == userId && state.TenantId == tenantId);
    }

    /// <summary>
    /// Cancels registered local streams associated with the client.
    /// </summary>
    public virtual void CloseConnectionsForClient(string clientId)
    {
        CloseConnectionsWhere(state => state.ClientId == clientId);
    }

    private void CloseConnectionsWhere(Func<SseConnectionState, bool> matches)
    {
        foreach (KeyValuePair<string, SseConnectionState> entry in _connections)
        {
            if (!matches(entry.Value))
            {
                continue;
            }

            if (!_cancellations.TryGetValue(entry.Key, out CancellationTokenSource? cancellation))
            {
                continue;
            }

            // Request cleanup may dispose the source concurrently with cancellation.
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public virtual ChannelReader<RealtimeEnvelope>? GetReader(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out SseConnectionState? state))
        {
            return state.Channel.Reader;
        }

        return null;
    }

    public virtual bool ShouldDeliver(SseConnectionState state, RealtimeEnvelope envelope, string module)
    {
        if (!state.Modules.Contains(module))
        {
            return false;
        }

        if (envelope.RequiredPermission is not null && !state.Permissions.Contains(envelope.RequiredPermission))
        {
            return false;
        }

        if (envelope.RequiredRole is not null && !state.Roles.Contains(envelope.RequiredRole))
        {
            return false;
        }

        if (envelope.TargetUserId is not null && envelope.TargetUserId != state.UserId)
        {
            return false;
        }

        return true;
    }

    public virtual IEnumerable<string> GetConnectionsForTenant(Guid tenantId)
    {
        return _connections
            .Where(kvp => kvp.Value.TenantId == tenantId)
            .Select(kvp => kvp.Key);
    }

    public virtual IEnumerable<string> GetConnectionForUser(string userId)
    {
        return _connections
            .Where(kvp => kvp.Value.UserId == userId)
            .Select(kvp => kvp.Key);
    }

    public virtual SseConnectionState? GetConnectionState(string connectionId)
    {
        _connections.TryGetValue(connectionId, out SseConnectionState? state);
        return state;
    }
}
