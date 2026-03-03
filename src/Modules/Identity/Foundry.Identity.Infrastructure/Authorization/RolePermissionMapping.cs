using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Infrastructure.Authorization;

public static class RolePermissionMapping
{
    private static readonly Dictionary<string, string[]> _rolePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = PermissionType.All.ToArray(),
        ["manager"] =
        [
            PermissionType.UsersRead,
            PermissionType.BillingRead,
            PermissionType.OrganizationsRead,
            PermissionType.OrganizationsManageMembers,
            PermissionType.ApiKeysRead,
            PermissionType.ApiKeysCreate,
            PermissionType.ApiKeysUpdate,
            PermissionType.ApiKeysDelete,
            PermissionType.SsoRead,
            PermissionType.ConfigurationManage,
        ],
        ["user"] =
        [
            PermissionType.OrganizationsRead,
        ]
    };

    public static IEnumerable<string> GetPermissions(IEnumerable<string> roles)
    {
        return roles
            .Where(r => _rolePermissions.ContainsKey(r))
            .SelectMany(r => _rolePermissions[r])
            .Distinct();
    }
}
