namespace Foundry.Shared.Kernel.Identity.Authorization;

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

    // Billing
    public const string BillingRead = "BillingRead";
    public const string BillingManage = "BillingManage";
    public const string InvoicesRead = "InvoicesRead";
    public const string InvoicesWrite = "InvoicesWrite";
    public const string PaymentsRead = "PaymentsRead";
    public const string PaymentsWrite = "PaymentsWrite";
    public const string SubscriptionsRead = "SubscriptionsRead";
    public const string SubscriptionsWrite = "SubscriptionsWrite";

    // Organizations
    public const string OrganizationsRead = "OrganizationsRead";
    public const string OrganizationsCreate = "OrganizationsCreate";
    public const string OrganizationsUpdate = "OrganizationsUpdate";
    public const string OrganizationsManageMembers = "OrganizationsManageMembers";

    // API Keys / Service Accounts
    public const string ApiKeysRead = "ApiKeysRead";
    public const string ApiKeysCreate = "ApiKeysCreate";
    public const string ApiKeysUpdate = "ApiKeysUpdate";
    public const string ApiKeysDelete = "ApiKeysDelete";

    // Notifications
    public const string NotificationsRead = "NotificationsRead";
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

    // Storage
    public const string StorageRead = "StorageRead";
    public const string StorageWrite = "StorageWrite";

    // API Key management
    public const string ApiKeyManage = "ApiKeyManage";

    // Scope management
    public const string ScopeRead = "ScopeRead";

    /// <summary>
    /// Returns all permission constants defined in this class.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = typeof(PermissionType)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();
}
