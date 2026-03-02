namespace Foundry.Communications.Application.Messaging.DTOs;

public sealed record ParticipantDto(
    Guid UserId,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LastReadAt,
    bool IsActive);
