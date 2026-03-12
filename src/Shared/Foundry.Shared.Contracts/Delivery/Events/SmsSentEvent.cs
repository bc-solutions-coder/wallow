namespace Foundry.Shared.Contracts.Delivery.Events;

/// <summary>
/// Published when an SMS is successfully sent.
/// </summary>
public sealed record SmsSentEvent : IntegrationEvent
{
    public required Guid SmsId { get; init; }
    public required Guid TenantId { get; init; }
    public required string ToNumber { get; init; }
    public required DateTime SentAt { get; init; }
}
