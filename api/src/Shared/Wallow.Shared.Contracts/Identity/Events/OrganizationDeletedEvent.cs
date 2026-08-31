namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published after an organization and everything Identity held for it are gone: tokens revoked,
/// OpenIddict applications and registered clients deleted, memberships, invitations, sessions,
/// settings and branding removed. Consumers: Identity (auth audit trail), Notifications (deletion
/// email to the admins that were), ApiKeys (revokes the tenant's keys), Branding (drops the
/// tenant's client brandings). Former members remain users; first-party consents are untouched.
/// </summary>
public sealed record OrganizationDeletedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required string OrganizationName { get; init; }

    /// <summary>The admin who typed the name and deleted the organization.</summary>
    public required Guid ActorId { get; init; }

    /// <summary>Admins' emails resolved before the rows died; empty sends nothing.</summary>
    public required IReadOnlyList<string> RecipientEmails { get; init; }
}
