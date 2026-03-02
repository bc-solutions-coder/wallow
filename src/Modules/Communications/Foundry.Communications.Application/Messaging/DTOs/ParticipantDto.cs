namespace Foundry.Communications.Application.Messaging.DTOs;

public sealed record ParticipantDto(
    Guid UserId,
    DateTime JoinedAt,
    DateTime? LastReadAt,
    bool IsActive);
