// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when an organization deletes one of its clients. Consumers: Identity (auth audit
/// trail), Branding (drops the client's branding and logo).
/// </summary>
public sealed record ClientDeletedEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required Guid OrganizationId { get; init; }

    /// <summary>Who deleted it.</summary>
    public required Guid ActorId { get; init; }
    public string? IpAddress { get; init; }
}
