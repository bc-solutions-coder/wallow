namespace Foundry.Communications.Application.Messaging.DTOs;

public sealed record ConversationDto(
    Guid Id,
    string Type,
    IReadOnlyList<ParticipantDto> Participants,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTime LastActivityAt);
