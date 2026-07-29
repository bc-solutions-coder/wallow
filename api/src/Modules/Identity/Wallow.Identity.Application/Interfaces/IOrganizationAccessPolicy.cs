namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Ownership lookup for organization-scoped endpoints. An organization IS the tenant, so creating
/// one mints a NEW tenant id that can never equal the creator's own — leaving the creator unable to
/// address the organization they just created through the ambient tenant check alone. The creator is
/// recorded as an <c>OrgMemberRole.Admin</c> member at creation time; that relationship, not any
/// role string, is what re-grants access to that one organization.
/// </summary>
public interface IOrganizationAccessPolicy
{
    /// <summary>
    /// Whether <paramref name="userId"/> is an admin member of <paramref name="organizationId"/>.
    /// Admin membership is minted only by creating the organization (<c>AddMemberAsync</c> always
    /// adds <c>OrgMemberRole.Member</c>), so this cannot be granted by an attacker to widen reach.
    /// </summary>
    Task<bool> IsOrganizationAdminAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
