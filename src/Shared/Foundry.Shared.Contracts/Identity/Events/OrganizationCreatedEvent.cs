// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when a new organization is created in Keycloak.
/// Consumers: Billing (setup org billing), Communications (org setup)
/// </summary>
public sealed record OrganizationCreatedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Name { get; init; }
    public string? Domain { get; init; }
    public required string CreatorEmail { get; init; }
}
