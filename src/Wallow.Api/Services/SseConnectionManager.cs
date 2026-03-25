using System.Collections.Concurrent;
using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

public class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseConnectionState> _connections = new();

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
    }

    public virtual void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
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
