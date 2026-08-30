using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

/// <summary>
/// One open stream: who holds it, in which tenant, what it may receive, and — when the token that
/// opened it was issued through a registered client — which client, so suspending that client
/// can hang the stream up.
/// </summary>
public sealed record SseConnectionState(
    string UserId,
    Guid TenantId,
    HashSet<string> Modules,
    HashSet<string> Permissions,
    HashSet<string> Roles,
    Channel<RealtimeEnvelope> Channel,
    string? ClientId = null);
