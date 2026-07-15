namespace Wallow.Shared.Contracts.Identity.Events;

public sealed record OrganizationDeletedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Name { get; init; }
}
