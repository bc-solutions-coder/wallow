namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when the platform operator lifts an organization's platform suspension. Nothing is
/// revoked back into place: people sign in again, and clients the organization suspended itself
/// stay suspended. Consumers: Identity (auth audit trail).
/// </summary>
public sealed record OrganizationReinstatedByPlatformEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }

    /// <summary>The global admin who lifted it.</summary>
    public required Guid ActorId { get; init; }
}
