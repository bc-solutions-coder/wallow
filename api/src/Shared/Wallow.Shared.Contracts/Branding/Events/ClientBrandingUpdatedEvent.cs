// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Branding.Events;

/// <summary>
/// Reports a client branding change for Identity auditing and display-name synchronization.
/// Synchronization must read the current name through <see cref="IClientBrandingProvider"/>
/// to avoid applying stale event payloads.
/// </summary>
public sealed record ClientBrandingUpdatedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    public required Guid ActorId { get; init; }

    /// <summary>
    /// Display name at the time of the write, for auditing only. Synchronization must read
    /// the current value to handle out-of-order delivery.
    /// </summary>
    public required string DisplayName { get; init; }

    public string? IpAddress { get; init; }
}
