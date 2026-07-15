namespace Wallow.Shared.Contracts.Identity.Events;

public sealed record OrganizationReactivatedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ReactivatedBy { get; init; }
}
