namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Reports deletion of the organization and its Identity-owned clients, memberships,
/// invitations, sessions, settings and branding, after credential revocation.
/// Identity audits it; Notifications emails former admins; ApiKeys revokes tenant keys;
/// Branding removes client branding. Former members remain users; first-party consents remain.
/// </summary>
public sealed record OrganizationDeletedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string OrganizationName { get; init; }

    /// <summary>
    /// The administrator who confirmed deletion.
    /// </summary>
    public required Guid ActorId { get; init; }

    /// <summary>
    /// Administrator emails captured before deletion; an empty list sends no email.
    /// </summary>
    public required IReadOnlyList<string> RecipientEmails { get; init; }
}
