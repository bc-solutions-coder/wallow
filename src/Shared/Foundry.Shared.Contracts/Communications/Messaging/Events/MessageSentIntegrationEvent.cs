namespace Foundry.Shared.Contracts.Communications.Messaging.Events;

public sealed record MessageSentIntegrationEvent : IntegrationEvent
{
    public required Guid ConversationId { get; init; }
    public required Guid MessageId { get; init; }
    public required Guid SenderId { get; init; }
    public required string Content { get; init; }
    public required DateTimeOffset SentAt { get; init; }
    public required Guid TenantId { get; init; }
}
