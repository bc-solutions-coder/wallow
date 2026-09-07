// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Reports a platform-imposed client suspension and credential revocation.
/// Identity audits the change; Notifications emails the owning organization's active owners.
/// </summary>
public sealed record ClientSuspendedByPlatformEvent : IntegrationEvent
{
    public required string ClientId { get; init; }
    public required string ClientName { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string OrganizationName { get; init; }

    /// <summary>The global admin who placed the suspension.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>
    /// The operator's reason, visible to the organization. Only the platform can lift the suspension.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>Active owners' emails; empty when there is nobody to tell, which sends nothing.</summary>
    public required IReadOnlyList<string> RecipientEmails { get; init; }
    public string? IpAddress { get; init; }
}
