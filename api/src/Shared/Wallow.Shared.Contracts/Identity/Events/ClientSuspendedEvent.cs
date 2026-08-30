// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization suspends one of its clients, ending every credential it holds. Consumers: Identity (auth audit trail).
/// </summary>
public sealed record ClientSuspendedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who suspended it.</summary>
    public required Guid ActorId { get; init; }
    public string? IpAddress { get; init; }
}
