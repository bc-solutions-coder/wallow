namespace Wallow.Shared.Kernel.Identity.Authorization;

public static class ScopePermissionMapper
{
    public static string? MapScopeToPermission(string scope)
    {
        return scope switch
        {
            // Identity - Users
            "users.read" => PermissionType.UsersRead,
            "users.write" => PermissionType.UsersUpdate,
            "users.manage" => PermissionType.UsersDelete,

            // Identity - Roles
            "roles.read" => PermissionType.RolesRead,
            "roles.write" => PermissionType.RolesUpdate,
            "roles.manage" => PermissionType.RolesDelete,

            // Identity - Organizations. OrganizationsDelete is deliberately absent: deleting
            // an organization is irreversible and requires typing its name back, so it stays a
            // human-only action — no OAuth scope grants it to a client or service account.
            "organizations.read" => PermissionType.OrganizationsRead,
            "organizations.write" => PermissionType.OrganizationsUpdate,
            "organizations.manage" => PermissionType.OrganizationsManageMembers,

            // Identity - API Keys
            "apikeys.read" => PermissionType.ApiKeysRead,
            "apikeys.write" => PermissionType.ApiKeysUpdate,
            "apikeys.manage" => PermissionType.ApiKeyManage,

            // Storage
            "storage.read" => PermissionType.StorageRead,
            "storage.write" => PermissionType.StorageWrite,

            // Announcements and Notifications
            "announcements.read" => PermissionType.AnnouncementRead,
            "announcements.manage" => PermissionType.AnnouncementManage,
            "changelog.manage" => PermissionType.ChangelogManage,
            "notifications.read" => PermissionType.NotificationRead,
            "notifications.write" => PermissionType.NotificationsWrite,

            // Configuration
            "configuration.read" => PermissionType.ConfigurationRead,
            "configuration.manage" => PermissionType.ConfigurationManage,

            // Inquiries
            "inquiries.read" => PermissionType.InquiriesRead,
            "inquiries.write" => PermissionType.InquiriesWrite,

            // Platform
            "webhooks.manage" => PermissionType.WebhooksManage,

            _ => null // Unknown scopes are ignored
        };
    }
}
