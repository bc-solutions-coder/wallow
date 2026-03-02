namespace Foundry.Communications.Api.Contracts.Messaging.Responses;

public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    string Status,
    DateTimeOffset SentAt);
