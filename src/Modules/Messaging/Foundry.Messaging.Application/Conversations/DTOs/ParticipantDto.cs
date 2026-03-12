using JetBrains.Annotations;

namespace Foundry.Messaging.Application.Conversations.DTOs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ParticipantDto(
    Guid UserId,
    DateTime JoinedAt,
    DateTime? LastReadAt,
    bool IsActive);
