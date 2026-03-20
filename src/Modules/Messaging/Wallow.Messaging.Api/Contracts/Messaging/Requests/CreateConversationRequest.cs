namespace Wallow.Messaging.Api.Contracts.Messaging.Requests;

public sealed record CreateConversationRequest(
    IReadOnlyList<Guid> ParticipantIds,
    string? Subject);
