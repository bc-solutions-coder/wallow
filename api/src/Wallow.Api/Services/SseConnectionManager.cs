using System.Collections.Concurrent;
using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

public class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseConnectionState> _connections = new();

    // Kept beside the state rather than inside it: the source is the manager's handle on a LIVE
    // request, and the state is a plain value the dispatcher and its tests construct freely.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    public virtual void AddConnection(
        string connectionId,
        string userId,
        Guid tenantId,
        HashSet<string> modules,
        HashSet<string> permissions,
        HashSet<string> roles)
    {
        Channel<RealtimeEnvelope> channel = Channel.CreateBounded<RealtimeEnvelope>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        SseConnectionState state = new(userId, tenantId, modules, permissions, roles, channel);
        _connections[connectionId] = state;

        // The manager owns the source for as long as the connection lives; RemoveConnection disposes it.
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
    /// The token that ends one stream from outside the request serving it. Completing the channel
    /// would stop deliveries but leave the endpoint heart-beating, so the stream would keep the
    /// roles and permissions it was opened with alive.
    /// </summary>
    public virtual CancellationToken GetCancellationToken(string connectionId)
    {
        return _cancellations.TryGetValue(connectionId, out CancellationTokenSource? cancellation)
            ? cancellation.Token
            : CancellationToken.None;
    }

    /// <summary>
    /// Ends every stream this person holds in this tenant. The endpoint owning each one wakes on
    /// the cancelled token, unregisters itself and returns, so the client has to reconnect and
    /// present its credential again rather than keep the roles it connected with.
    /// </summary>
    public virtual void CloseConnectionsForUser(string userId, Guid tenantId)
    {
        foreach (KeyValuePair<string, SseConnectionState> entry in _connections)
        {
            if (entry.Value.UserId != userId || entry.Value.TenantId != tenantId)
            {
                continue;
            }

            if (!_cancellations.TryGetValue(entry.Key, out CancellationTokenSource? cancellation))
            {
                continue;
            }

            // The owning request disposes this source through RemoveConnection the moment it
            // observes the cancellation, so a race here is a no-op rather than a fault.
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
