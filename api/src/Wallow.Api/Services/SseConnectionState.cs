using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

/// <summary>
/// Connection-time user, tenant, subscriptions, permissions, and optional client id for one SSE stream.
/// </summary>
public sealed record SseConnectionState(
    string UserId,
    Guid TenantId,
    HashSet<string> Modules,
    HashSet<string> Permissions,
    HashSet<string> Roles,
    Channel<RealtimeEnvelope> Channel,
    string? ClientId = null);
