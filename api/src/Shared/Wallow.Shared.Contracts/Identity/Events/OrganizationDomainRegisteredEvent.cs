// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a domain is registered for an organization.
/// Consumers: Notifications (domain verification instructions)
/// </summary>
public sealed record OrganizationDomainRegisteredEvent : IntegrationEvent
{
    public required Guid OrganizationDomainId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Domain { get; init; }
}
