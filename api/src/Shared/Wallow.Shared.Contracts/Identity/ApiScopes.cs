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

        // Announcements and Notifications
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

        // Platform
        "webhooks.manage"
    };

    // The OIDC login scopes docs/integrations/bff-pattern.md instructs integrators to request.
    // Always grantable to an organization-registered application; API scopes are opt-in and
    // gated by the scope catalog.
    public static readonly IReadOnlySet<string> LoginScopes = new HashSet<string>
    {
        "openid",
        "profile",
        "email",
        "offline_access"
    };
}
