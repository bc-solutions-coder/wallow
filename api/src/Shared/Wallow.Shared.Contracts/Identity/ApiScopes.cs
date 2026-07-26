namespace Wallow.Shared.Contracts.Identity;

public static class ApiScopes
{
    public static readonly IReadOnlySet<string> ValidScopes = new HashSet<string>
    {
        // Identity - Users
        "users.read",
        "users.write",
        "users.manage",

        // Identity - Roles
        "roles.read",
        "roles.write",
        "roles.manage",

        // Identity - Organizations
        "organizations.read",
        "organizations.write",
        "organizations.manage",

        // Identity - API Keys
        "apikeys.read",
        "apikeys.write",
        "apikeys.manage",

        // Storage
        "storage.read",
        "storage.write",

        // Communications
        "announcements.read",
        "announcements.manage",
        "changelog.manage",
        "notifications.read",
        "notifications.write",

        // Configuration
        "configuration.read",
        "configuration.manage",

        // Inquiries
        "inquiries.read",
        "inquiries.write",

        // Identity - Service Accounts
        "serviceaccounts.read",
        "serviceaccounts.write",
        "serviceaccounts.manage",

        // Platform
        "webhooks.manage"
    };

    public static readonly IReadOnlySet<string> DeveloperAppScopes = new HashSet<string>
    {
        "inquiries.read",
        "inquiries.write",
        "announcements.read",
        "storage.read"
    };

    // The OIDC login scopes docs/integrations/bff-pattern.md instructs integrators to request.
    // A self-service app may request these in addition to DeveloperAppScopes.
    public static readonly IReadOnlySet<string> LoginScopes = new HashSet<string>
    {
        "openid",
        "profile",
        "email",
        "offline_access"
    };
}
