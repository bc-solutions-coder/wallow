namespace Wallow.Shared.Contracts.Branding.Events;

/// <summary>
/// Published when a client's branding configuration is updated.
/// Consumers: Identity (update OIDC client metadata), Web (refresh cached branding)
/// </summary>
public sealed record ClientBrandingUpdatedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required string DisplayName { get; init; }
    public required string? Tagline { get; init; }
    public required Guid TenantId { get; init; }
}
