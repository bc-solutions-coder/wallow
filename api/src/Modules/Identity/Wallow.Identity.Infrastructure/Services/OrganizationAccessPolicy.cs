using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Resolves cross-organization reach from the roles the caller's membership of that one
/// organization grants. The membership reads run before any tenant is resolved and constrain only
/// on their own parameters, which is what makes a creator's just-created org resolvable here.
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
