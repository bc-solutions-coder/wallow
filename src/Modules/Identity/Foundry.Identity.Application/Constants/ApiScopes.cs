namespace Foundry.Identity.Application.Constants;

public static class ApiScopes
{
    public static readonly IReadOnlySet<string> ValidScopes = new HashSet<string>
    {
        // Billing
        "billing.read",
        "billing.manage",
        "invoices.read",
        "invoices.write",
        "payments.read",
        "payments.write",
        "subscriptions.read",
        "subscriptions.write",

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

        // Identity - SSO/SCIM
        "sso.read",
        "sso.manage",
        "scim.manage",

        // Storage
        "storage.read",
        "storage.write",

        // Communications
        "messaging.access",
        "announcements.read",
        "announcements.manage",
        "changelog.manage",
        "notifications.read",
        "notifications.write",

        // Configuration
        "configuration.read",
        "configuration.manage",

        // Showcases
        "showcases.read",
        "showcases.manage",

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
}
