using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace Wallow.Shared.Kernel.Identity.Authorization;

public static class RolePermissionMapping
{
    private static readonly FrozenDictionary<string, string[]> _rolePermissions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] =
        [
            PermissionType.UsersRead,
            PermissionType.UsersCreate,
            PermissionType.UsersUpdate,
            PermissionType.UsersDelete,
            PermissionType.RolesRead,
            PermissionType.RolesCreate,
            PermissionType.RolesUpdate,
            PermissionType.RolesDelete,
            PermissionType.OrganizationsRead,
            PermissionType.OrganizationsUpdate,
            PermissionType.OrganizationsDelete,
            PermissionType.OrganizationsManageMembers,
            PermissionType.OrganizationClientsManage,
            PermissionType.ApiKeysRead,
            PermissionType.ApiKeysCreate,
            PermissionType.ApiKeysUpdate,
            PermissionType.ApiKeysDelete,
            PermissionType.NotificationRead,
            PermissionType.NotificationsWrite,
            PermissionType.WebhooksManage,
            PermissionType.AdminAccess,
            PermissionType.SystemSettings,
            PermissionType.ConfigurationRead,
            PermissionType.ConfigurationManage,
            PermissionType.EmailPreferenceManage,
            PermissionType.AnnouncementRead,
            PermissionType.AnnouncementManage,
            PermissionType.StorageRead,
            PermissionType.StorageWrite,
            PermissionType.ApiKeyManage,
            PermissionType.ScopeRead,
            PermissionType.PushRead,
            PermissionType.PushConfigWrite,
            PermissionType.InquiriesRead,
            PermissionType.InquiriesWrite,
        ],
        ["manager"] =
        [
            PermissionType.UsersRead,
            PermissionType.OrganizationsRead,
            PermissionType.OrganizationsManageMembers,
            PermissionType.OrganizationClientsManage,
            // Registering a client means choosing its scopes, so the catalog read
            // travels with the manage permission.
            PermissionType.ScopeRead,
            PermissionType.ApiKeysRead,
            PermissionType.ApiKeysCreate,
            PermissionType.ApiKeysUpdate,
            PermissionType.ApiKeysDelete,
            PermissionType.ConfigurationManage,
            PermissionType.InquiriesRead,
        ],
        // Members may read organization data but cannot administer it by default.
        // Creating an organization requires no organization permission.
        ["user"] =
        [
            PermissionType.OrganizationsRead,
            PermissionType.NotificationRead,
            PermissionType.EmailPreferenceManage,
            PermissionType.AnnouncementRead,
            PermissionType.StorageRead,
            PermissionType.StorageWrite,
            PermissionType.ApiKeysRead,
            PermissionType.ApiKeysCreate,
            PermissionType.InquiriesWrite,
        ]
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string[]> _cache = new();

    public static IEnumerable<string> GetPermissions(IEnumerable<string> roles)
    {
        string cacheKey = string.Join("|", roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));

        return _cache.GetOrAdd(cacheKey, _ => roles
            .Where(r => _rolePermissions.ContainsKey(r))
            .SelectMany(r => _rolePermissions[r])
            .Distinct()
            .ToArray());
    }
}
