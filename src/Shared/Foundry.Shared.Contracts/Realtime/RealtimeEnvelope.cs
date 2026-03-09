using JetBrains.Annotations;

namespace Foundry.Shared.Contracts.Realtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record RealtimeEnvelope(
    string Type,
    string Module,
    object Payload,
    DateTime Timestamp,
    string? CorrelationId = null)
{
    public static RealtimeEnvelope Create(string module, string type, object payload, string? correlationId = null)
        => new(type, module, payload, DateTime.UtcNow, correlationId);
}
