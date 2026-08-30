// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when the platform operator suspends a client over the owning organization's head,
/// ending every credential it holds. Consumers: Identity (auth audit trail), Notifications
/// (suspension email to the owning organization's active owners).
/// </summary>
public sealed record ClientSuspendedByPlatformEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required string ClientName { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string OrganizationName { get; init; }

    /// <summary>The global admin who placed the suspension.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>The operator's reason, readable by the organization but not liftable by it.</summary>
    public required string Reason { get; init; }

    /// <summary>Active owners' emails; empty when there is nobody to tell, which sends nothing.</summary>
    public required IReadOnlyList<string> RecipientEmails { get; init; }
    public string? IpAddress { get; init; }
}
