// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Delivery.Events;

public sealed record SendPushRequestedEvent : IntegrationEvent
{
    public required Guid RecipientId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string NotificationType { get; init; }
    public required bool IsCritical { get; init; }
}
