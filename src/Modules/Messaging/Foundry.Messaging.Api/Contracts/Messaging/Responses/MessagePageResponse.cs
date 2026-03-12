namespace Foundry.Messaging.Api.Contracts.Messaging.Responses;

public sealed record MessagePageResponse(
    IReadOnlyList<MessageResponse> Items,
    Guid? NextCursor,
    bool HasMore);
