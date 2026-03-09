using JetBrains.Annotations;

namespace Foundry.Communications.Application.Messaging.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ParticipantDto(
    Guid UserId,
    DateTime JoinedAt,
    DateTime? LastReadAt,
    bool IsActive);
