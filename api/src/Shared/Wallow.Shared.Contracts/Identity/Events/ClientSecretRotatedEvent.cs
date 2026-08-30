// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization rotates one of its clients' secrets. Consumers: Identity (auth
/// audit trail).
/// </summary>
public sealed record ClientSecretRotatedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who rotated it.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>Whether the rotation also ended every token the client had been issued.</summary>
    public required bool ActiveTokensRevoked { get; init; }
    public string? IpAddress { get; init; }
}
