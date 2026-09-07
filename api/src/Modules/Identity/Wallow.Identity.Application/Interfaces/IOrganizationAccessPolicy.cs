namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Checks permissions held through membership in the addressed organization,
/// independently of the caller's ambient tenant.
/// </summary>
public interface IOrganizationAccessPolicy
{
    /// <summary>
    /// Expands roles from the active membership in this organization. Callers must pass
    /// the endpoint's required permission; membership alone does not authorize access.
    /// </summary>
    Task<bool> HasPermissionInOrganizationAsync(
        Guid organizationId,
        Guid userId,
        string requiredPermission,
        CancellationToken ct = default);
}
