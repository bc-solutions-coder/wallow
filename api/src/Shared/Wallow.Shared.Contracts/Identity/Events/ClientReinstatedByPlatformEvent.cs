// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when the platform operator lifts a client's platform suspension. The client serves
/// again unless the organization's own suspension still stands. Consumers: Identity (auth audit trail).
/// </summary>
public sealed record ClientReinstatedByPlatformEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>The global admin who lifted it.</summary>
    public required Guid ActorId { get; init; }
    public string? IpAddress { get; init; }
}
