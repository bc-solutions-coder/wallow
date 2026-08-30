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
            PermissionType.ChangelogManage,
            PermissionType.StorageRead,
            PermissionType.StorageWrite,
            PermissionType.ApiKeyManage,
            PermissionType.ScopeRead,
            PermissionType.ServiceAccountsRead,
            PermissionType.ServiceAccountsWrite,
            PermissionType.ServiceAccountsManage,
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
        // Read-only on organizations. A plain member holding OrganizationsUpdate can rewrite the
        // settings of any organization they belong to; granting it by default hands every member
        // the administrative surface the per-organization roles exist to withhold. Founding an
        // organization needs no permission at all: any account holder may, even with a token that
        // names no organization, so there is nothing here to grant or withhold for it.
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
