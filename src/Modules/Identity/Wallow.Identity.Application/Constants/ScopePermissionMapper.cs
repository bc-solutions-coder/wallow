using Wallow.Shared.Kernel.Identity.Authorization;

namespace Wallow.Identity.Application.Constants;

public static class ScopePermissionMapper
{
    public static string? MapScopeToPermission(string scope)
    {
        return scope switch
        {
            // Billing
            "billing.read" => PermissionType.BillingRead,
            "billing.manage" => PermissionType.BillingManage,
            "invoices.read" => PermissionType.InvoicesRead,
            "invoices.write" => PermissionType.InvoicesWrite,
            "payments.read" => PermissionType.PaymentsRead,
            "payments.write" => PermissionType.PaymentsWrite,
            "subscriptions.read" => PermissionType.SubscriptionsRead,
            "subscriptions.write" => PermissionType.SubscriptionsWrite,

            // Identity - Users
            "users.read" => PermissionType.UsersRead,
            "users.write" => PermissionType.UsersUpdate,
            "users.manage" => PermissionType.UsersDelete,

            // Identity - Roles
            "roles.read" => PermissionType.RolesRead,
            "roles.write" => PermissionType.RolesUpdate,
            "roles.manage" => PermissionType.RolesDelete,

            // Identity - Organizations
            "organizations.read" => PermissionType.OrganizationsRead,
            "organizations.write" => PermissionType.OrganizationsUpdate,
            "organizations.manage" => PermissionType.OrganizationsManageMembers,

            // Identity - API Keys
            "apikeys.read" => PermissionType.ApiKeysRead,
            "apikeys.write" => PermissionType.ApiKeysUpdate,
            "apikeys.manage" => PermissionType.ApiKeyManage,

            // Identity - SSO/SCIM
            "sso.read" => PermissionType.SsoRead,
            "sso.manage" => PermissionType.SsoManage,
            "scim.manage" => PermissionType.ScimManage,

            // Storage
            "storage.read" => PermissionType.StorageRead,
            "storage.write" => PermissionType.StorageWrite,

            // Communications
            "messaging.access" => PermissionType.MessagingAccess,
            "announcements.read" => PermissionType.AnnouncementRead,
            "announcements.manage" => PermissionType.AnnouncementManage,
            "changelog.manage" => PermissionType.ChangelogManage,
            "notifications.read" => PermissionType.NotificationsRead,
            "notifications.write" => PermissionType.NotificationsWrite,

            // Configuration
            "configuration.read" => PermissionType.ConfigurationRead,
            "configuration.manage" => PermissionType.ConfigurationManage,

            // Inquiries
            "inquiries.read" => PermissionType.InquiriesRead,
            "inquiries.write" => PermissionType.InquiriesWrite,

            // Identity - Service Accounts
            "serviceaccounts.read" => PermissionType.ServiceAccountsRead,
            "serviceaccounts.write" => PermissionType.ServiceAccountsWrite,
            "serviceaccounts.manage" => PermissionType.ServiceAccountsManage,

            // Platform
            "webhooks.manage" => PermissionType.WebhooksManage,

            _ => null // Unknown scopes are ignored
        };
    }
}
