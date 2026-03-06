using System.Collections.Frozen;
using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Infrastructure.Authorization;

public static class RolePermissionMapping
{
    private static readonly FrozenDictionary<string, string[]> _rolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
            PermissionType.ShowcasesRead,
            PermissionType.ShowcasesManage,
        ],
        ["user"] =
        [
            PermissionType.OrganizationsRead,
            PermissionType.MessagingAccess,
            PermissionType.NotificationRead,
            PermissionType.EmailPreferenceManage,
            PermissionType.AnnouncementRead,
            PermissionType.StorageRead,
            PermissionType.StorageWrite,
            PermissionType.ShowcasesRead,
        ]
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> GetPermissions(IEnumerable<string> roles)
    {
        return roles
            .Where(r => _rolePermissions.ContainsKey(r))
            .SelectMany(r => _rolePermissions[r])
            .Distinct();
    }
}
