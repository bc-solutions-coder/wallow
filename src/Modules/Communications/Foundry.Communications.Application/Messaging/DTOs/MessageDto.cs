namespace Foundry.Communications.Application.Messaging.DTOs;

public sealed record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    DateTimeOffset SentAt,
    string Status);
