// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization registers a client (a developer application or a service
/// account). Consumers: Identity (auth audit trail).
/// </summary>
public sealed record ClientRegisteredEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who registered it.</summary>
    public required Guid ActorId { get; init; }
    public string? IpAddress { get; init; }
}
