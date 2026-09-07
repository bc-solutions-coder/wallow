using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Expands roles from the user's active membership in the addressed organization.
/// The lookup is independent of the ambient tenant.
/// </summary>
public sealed class OrganizationAccessPolicy(
    IMembershipRoleResolver roleResolver) : IOrganizationAccessPolicy
{
    public async Task<bool> HasPermissionInOrganizationAsync(
        Guid organizationId,
        Guid userId,
        string requiredPermission,
        CancellationToken ct = default)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(requiredPermission))
        {
            return false;
        }

        IReadOnlyList<string> roleNames = await roleResolver.GetRoleNamesAsync(userId, organizationId, ct);
        if (roleNames.Count == 0)
        {
            return false;
        }

        return RolePermissionMapping.GetPermissions(roleNames)
            .Contains(requiredPermission, StringComparer.OrdinalIgnoreCase);
    }
}
