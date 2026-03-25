namespace Wallow.Shared.Contracts.Identity.Events;

public sealed record OrganizationArchivedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ArchivedBy { get; init; }
}
