using JetBrains.Annotations;

namespace Wallow.Shared.Contracts.Realtime;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record UserPresence(
    string UserId,
    string? DisplayName,
    IReadOnlyList<string> ConnectionIds,
    IReadOnlyList<string> CurrentPages);
