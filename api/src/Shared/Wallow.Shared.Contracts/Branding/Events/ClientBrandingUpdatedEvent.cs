// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Branding.Events;

/// <summary>
/// Published when an organization changes a client's branding — display name, tagline, logo or
/// theme. Consumers: Identity (auth audit trail, and syncing the OpenIddict application's
/// display name so every screen shows the one end-user-facing name). The event is a trigger,
/// not a payload: the sync pulls the row's current name through
/// <see cref="IClientBrandingProvider"/> so redelivery and reordering are harmless.
/// </summary>
public sealed record ClientBrandingUpdatedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who changed it.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>
    /// The end-user-facing display name this write set — audit payload only. Synchronization
    /// consumers must not apply it; an out-of-order delivery would freeze an older value.
    /// </summary>
    public required string DisplayName { get; init; }

    public string? IpAddress { get; init; }
}
