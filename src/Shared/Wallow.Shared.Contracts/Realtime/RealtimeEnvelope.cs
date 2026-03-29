using JetBrains.Annotations;

namespace Wallow.Shared.Contracts.Realtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record RealtimeEnvelope(
    string Type,
    string Module,
    object Payload,
    DateTime Timestamp,
    string? CorrelationId = null,
    string? RequiredPermission = null,
    string? RequiredRole = null,
    string? TargetUserId = null)
{
    public static RealtimeEnvelope Create(string module, string type, object payload, string? correlationId = null)
        => new(type, module, payload, DateTime.UtcNow, correlationId);
}
