namespace Foundry.Shared.Contracts.Notifications.Events;

public sealed record SendNotificationRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public required bool IsCritical { get; init; }
}
