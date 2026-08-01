namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Ownership lookup for organization-scoped endpoints. An organization IS the tenant, so creating
/// one mints a NEW tenant id that can never equal the creator's own — leaving the creator unable to
/// address the organization they just created through the ambient tenant check alone. The creator
/// gets an Active membership carrying the admin role; that per-organization relationship, not any
/// claim on the caller's token, is what re-grants access to that one organization.
/// </summary>
public interface IOrganizationAccessPolicy
{
    /// <summary>
    /// Whether <paramref name="userId"/> holds the admin role IN <paramref name="organizationId"/>.
    /// The grant lives on the membership, so being an admin elsewhere confers nothing here.
    /// </summary>
    Task<bool> IsOrganizationAdminAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
}
