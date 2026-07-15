using System.Threading.Channels;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Api.Services;

public sealed record SseConnectionState(
    string UserId,
    Guid TenantId,
    HashSet<string> Modules,
    HashSet<string> Permissions,
    HashSet<string> Roles,
    Channel<RealtimeEnvelope> Channel);
