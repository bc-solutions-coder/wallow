namespace Wallow.Shared.Kernel.Identity.Authorization;

/// <summary>
/// System permission types for RBAC.
/// String constants allow modules to define their own permissions without modifying this class.
/// </summary>
public static class PermissionType
{
    public const string None = "None";

    // User management
    public const string UsersRead = "UsersRead";
    public const string UsersCreate = "UsersCreate";
    public const string UsersUpdate = "UsersUpdate";
    public const string UsersDelete = "UsersDelete";

    // Role management
    public const string RolesRead = "RolesRead";
    public const string RolesCreate = "RolesCreate";
    public const string RolesUpdate = "RolesUpdate";
    public const string RolesDelete = "RolesDelete";

    // Organizations
    public const string OrganizationsRead = "OrganizationsRead";
    public const string OrganizationsCreate = "OrganizationsCreate";
    public const string OrganizationsUpdate = "OrganizationsUpdate";
    public const string OrganizationsManageMembers = "OrganizationsManageMembers";

    // API Keys
    public const string ApiKeysRead = "ApiKeysRead";
    public const string ApiKeysCreate = "ApiKeysCreate";
    public const string ApiKeysUpdate = "ApiKeysUpdate";
    public const string ApiKeysDelete = "ApiKeysDelete";

    // Notifications
    public const string NotificationsWrite = "NotificationsWrite";

    // Webhooks
    public const string WebhooksManage = "WebhooksManage";

    // SSO/SCIM
    public const string SsoRead = "SsoRead";
    public const string SsoManage = "SsoManage";
    public const string ScimManage = "ScimManage";

    // Admin
    public const string AdminAccess = "AdminAccess";
    public const string SystemSettings = "SystemSettings";

    // Configuration
    public const string ConfigurationRead = "ConfigurationRead";
    public const string ConfigurationManage = "ConfigurationManage";

    // Communications
    public const string NotificationRead = "NotificationRead";
    public const string EmailPreferenceManage = "EmailPreferenceManage";
    public const string MessagingAccess = "MessagingAccess";
    public const string AnnouncementRead = "AnnouncementRead";
    public const string AnnouncementManage = "AnnouncementManage";

    public const string ChangelogManage = "ChangelogManage";

    // Push notifications
    public const string PushRead = "PushRead";
    public const string PushConfigWrite = "PushConfigWrite";

    // Storage
    public const string StorageRead = "StorageRead";
    public const string StorageWrite = "StorageWrite";

    // API Key management
    public const string ApiKeyManage = "ApiKeyManage";

    // Inquiries
    public const string InquiriesRead = "InquiriesRead";
    public const string InquiriesWrite = "InquiriesWrite";

    // Service Accounts
    public const string ServiceAccountsRead = "ServiceAccountsRead";
    public const string ServiceAccountsWrite = "ServiceAccountsWrite";
    public const string ServiceAccountsManage = "ServiceAccountsManage";

    // Scope management
    public const string ScopeRead = "ScopeRead";

    /// <summary>
    /// Returns all permission constants defined in this class.
    /// Uses reflection to auto-discover permissions at startup. Not a hot path.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = typeof(PermissionType)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();
}
