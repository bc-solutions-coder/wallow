namespace Wallow.Shared.Contracts.Notifications.Events;

/// <summary>
/// Published when a notification is created and ready for delivery.
/// </summary>
public sealed record NotificationCreatedEvent : IntegrationEvent
{
    public required Guid NotificationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public required DateTime CreatedAt { get; init; }
}
