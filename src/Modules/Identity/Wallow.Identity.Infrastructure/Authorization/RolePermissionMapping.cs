using System.Collections.Concurrent;
using System.Collections.Frozen;
using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Infrastructure.Authorization;

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
            PermissionType.BillingRead,
            PermissionType.BillingManage,
            PermissionType.InvoicesRead,
            PermissionType.InvoicesWrite,
            PermissionType.PaymentsRead,
            PermissionType.PaymentsWrite,
            PermissionType.SubscriptionsRead,
            PermissionType.SubscriptionsWrite,
            PermissionType.OrganizationsRead,
            PermissionType.OrganizationsCreate,
            PermissionType.OrganizationsUpdate,
            PermissionType.OrganizationsManageMembers,
            PermissionType.ApiKeysRead,
            PermissionType.ApiKeysCreate,
            PermissionType.ApiKeysUpdate,
            PermissionType.ApiKeysDelete,
            PermissionType.NotificationsRead,
            PermissionType.NotificationsWrite,
            PermissionType.WebhooksManage,
            PermissionType.SsoRead,
            PermissionType.SsoManage,
            PermissionType.ScimManage,
            PermissionType.AdminAccess,
            PermissionType.SystemSettings,
            PermissionType.ConfigurationRead,
            PermissionType.ConfigurationManage,
            PermissionType.NotificationRead,
            PermissionType.EmailPreferenceManage,
            PermissionType.MessagingAccess,
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
            PermissionType.BillingRead,
            PermissionType.OrganizationsRead,
            PermissionType.OrganizationsManageMembers,
            PermissionType.ApiKeysRead,
            PermissionType.ApiKeysCreate,
            PermissionType.ApiKeysUpdate,
            PermissionType.ApiKeysDelete,
            PermissionType.SsoRead,
            PermissionType.ConfigurationManage,
            PermissionType.InquiriesRead,
        ],
        ["user"] =
        [
            PermissionType.OrganizationsRead,
            PermissionType.OrganizationsCreate,
            PermissionType.OrganizationsUpdate,
            PermissionType.MessagingAccess,
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
