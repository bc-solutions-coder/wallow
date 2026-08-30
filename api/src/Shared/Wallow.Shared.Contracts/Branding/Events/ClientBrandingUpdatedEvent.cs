// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Branding.Events;

/// <summary>
/// Published when an organization changes a client's branding — display name, tagline, logo or
/// theme. Consumers: Identity (auth audit trail, and rewriting the OpenIddict application's
/// display name so every screen shows the one end-user-facing name).
/// </summary>
public sealed record ClientBrandingUpdatedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who changed it.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>The end-user-facing display name after the update.</summary>
    public required string DisplayName { get; init; }

    public string? IpAddress { get; init; }
}
