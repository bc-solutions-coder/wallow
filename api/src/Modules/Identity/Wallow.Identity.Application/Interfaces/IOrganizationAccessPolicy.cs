namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Cross-organization reach for organization-scoped endpoints. An organization IS the tenant, so
/// creating one mints a NEW tenant id that can never equal the creator's own — leaving the creator
/// unable to address the organization they just created through the ambient tenant check alone. An
/// Active membership in that organization re-grants the reach, and the roles that membership
/// carries decide how far it reaches.
/// </summary>
public interface IOrganizationAccessPolicy
{
    /// <summary>
    /// Whether <paramref name="userId"/> holds <paramref name="requiredPermission"/> IN
    /// <paramref name="organizationId"/>, expanded from the roles that organization's Active
    /// membership grants. Holding the permission somewhere else confers nothing here.
    /// </summary>
    /// <remarks>
    /// Callers pass the permission the endpoint itself demands, so read reach and destroy reach are
    /// separate answers. Do not collapse this to "a membership exists": memberships become
    /// self-mintable once an organization opens enrollment, and a self-enrolled visitor would then
    /// read the full roster, every member email, and the organization's own settings.
    /// </remarks>
    Task<bool> HasPermissionInOrganizationAsync(
        Guid organizationId,
        Guid userId,
        string requiredPermission,
        CancellationToken ct = default);
}
