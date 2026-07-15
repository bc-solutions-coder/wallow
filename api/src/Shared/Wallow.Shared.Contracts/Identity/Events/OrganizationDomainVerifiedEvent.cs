// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization's domain is verified.
/// Consumers: Notifications (domain verified confirmation)
/// </summary>
public sealed record OrganizationDomainVerifiedEvent : IntegrationEvent
{
    public required Guid OrganizationDomainId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Domain { get; init; }
}
