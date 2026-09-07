namespace Wallow.Shared.Contracts.Delivery.Events;

public sealed record PushSentEvent : IntegrationEvent
{
    public required Guid PushId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid RecipientId { get; init; }
    public required DateTime SentAt { get; init; }
}
