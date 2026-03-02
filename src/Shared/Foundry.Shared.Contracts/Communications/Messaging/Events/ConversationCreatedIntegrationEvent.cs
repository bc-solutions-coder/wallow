namespace Foundry.Shared.Contracts.Communications.Messaging.Events;

/// <summary>
/// Published when a new conversation is created.
/// </summary>
public sealed record ConversationCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required IReadOnlyList<Guid> ParticipantIds { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required Guid TenantId { get; init; }
}
