using Foundry.Communications.Application.Messaging.DTOs;

namespace Foundry.Communications.Api.Contracts.Messaging.Responses;

public sealed record ConversationResponse(
    Guid Id,
    string Type,
    IReadOnlyList<ParticipantDto> Participants,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTimeOffset LastActivityAt);
