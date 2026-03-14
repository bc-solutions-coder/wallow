namespace Foundry.Identity.Application.Constants;

public static class ApiScopes
{
    public static readonly IReadOnlySet<string> ValidScopes = new HashSet<string>
    {
        "invoices.read",
        "invoices.write",
        "payments.read",
        "payments.write",
        "subscriptions.read",
        "subscriptions.write",
        "users.read",
        "users.write",
        "notifications.read",
        "notifications.write",
        "webhooks.manage",
        "showcases.read",
        "inquiries.read",
        "inquiries.write"
    };
}
