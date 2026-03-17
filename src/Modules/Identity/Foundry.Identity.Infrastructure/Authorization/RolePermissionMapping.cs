using System.Collections.Frozen;
using Foundry.Shared.Kernel.Identity.Authorization;

namespace Foundry.Identity.Infrastructure.Authorization;

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
            PermissionType.ShowcasesRead,
            PermissionType.ShowcasesManage,
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
            PermissionType.ShowcasesRead,
            PermissionType.ShowcasesManage,
            PermissionType.InquiriesRead,
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
            PermissionType.InquiriesWrite,
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
