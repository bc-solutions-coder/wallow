namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when the platform operator suspends an organization: every bound client's and every
/// member's tokens are revoked, and every change to the organization is refused while it stands.
/// Consumers: Identity (auth audit trail), Notifications (suspension email to the active owners).
/// </summary>
public sealed record OrganizationSuspendedByPlatformEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string OrganizationName { get; init; }

    /// <summary>The global admin who placed the suspension.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>The operator's reason, readable by the organization but not liftable by it.</summary>
    public required string Reason { get; init; }

    /// <summary>Active owners' emails; empty when there is nobody to tell, which sends nothing.</summary>
    public required IReadOnlyList<string> RecipientEmails { get; init; }
}
