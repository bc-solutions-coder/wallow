namespace Foundry.Shared.Kernel.Identity.Authorization;

/// <summary>
/// System permission types for RBAC
/// </summary>
public enum PermissionType
{
    /// <summary>
    /// No permission assigned (default value).
    /// </summary>
    None = 0,

    // User management
    UsersRead = 100,
    UsersCreate = 101,
    UsersUpdate = 102,
    UsersDelete = 103,

    // Role management
    RolesRead = 200,
    RolesCreate = 201,
    RolesUpdate = 202,
    RolesDelete = 203,

    // Billing
    BillingRead = 500,
    BillingManage = 501,
    InvoicesRead = 502,
    InvoicesWrite = 503,
    PaymentsRead = 504,
    PaymentsWrite = 505,
    SubscriptionsRead = 506,
    SubscriptionsWrite = 507,

    // Organizations
    OrganizationsRead = 600,
    OrganizationsCreate = 601,
    OrganizationsUpdate = 602,
    OrganizationsManageMembers = 603,

    // API Keys / Service Accounts
    ApiKeysRead = 700,
    ApiKeysCreate = 701,
    ApiKeysUpdate = 702,
    ApiKeysDelete = 703,

    // Notifications
    NotificationsRead = 750,
    NotificationsWrite = 751,

    // Webhooks
    WebhooksManage = 850,

    // SSO/SCIM
    SsoRead = 860,
    SsoManage = 861,
    ScimManage = 862,

    // Admin
    AdminAccess = 900,
    SystemSettings = 901,

    // Configuration
    ConfigurationManage = 950,

    // Communications
    NotificationRead = 800,
    EmailPreferenceManage = 801,
    MessagingAccess = 802,
    AnnouncementRead = 803,

    // Storage
    StorageRead = 300,
    StorageWrite = 301,

    // API Key management
    ApiKeyManage = 704,

    // Scope management
    ScopeRead = 710
}
