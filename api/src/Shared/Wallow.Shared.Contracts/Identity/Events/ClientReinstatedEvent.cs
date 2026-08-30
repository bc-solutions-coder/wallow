// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization reinstates a suspended client. Consumers: Identity (auth audit trail).
/// </summary>
public sealed record ClientReinstatedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who reinstated it.</summary>
    public required Guid ActorId { get; init; }
    public string? IpAddress { get; init; }
}
