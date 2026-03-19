using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Infrastructure.Data;

/// <summary>
/// Seeds default API scopes that can be assigned to service accounts.
/// </summary>
public sealed partial class ApiScopeSeeder(ILogger<ApiScopeSeeder> logger)
{

    /// <summary>
    /// Seeds default API scopes to the database.
    /// Idempotent - only adds scopes that don't already exist.
    /// </summary>
    public async Task SeedAsync(IdentityDbContext context, CancellationToken ct = default)
    {
        List<string> existingCodes = await context.ApiScopes
            .Select(s => s.Code)
            .ToListAsync(ct);

        List<ApiScope> newScopes = GetDefaultScopes()
            .Where(s => !existingCodes.Contains(s.Code))
            .ToList();

        if (newScopes.Count == 0)
        {
            LogAllScopesExist();
            return;
        }

        LogSeedingScopes(newScopes.Count);

        foreach (ApiScope scope in newScopes)
        {
            context.ApiScopes.Add(scope);
        }

        await context.SaveChangesAsync(ct);
        LogScopesSeeded(newScopes.Count);
    }

    private static IEnumerable<ApiScope> GetDefaultScopes()
    {
        // Billing scopes (read-only is default for most integrations)
        yield return ApiScope.Create("billing.read", "Read Billing", "Billing",
            "Access to read billing data", isDefault: true);
        yield return ApiScope.Create("billing.manage", "Manage Billing", "Billing",
            "Access to manage billing settings and configuration");
        yield return ApiScope.Create("invoices.read", "Read Invoices", "Billing",
            "Access to read invoices and invoice data", isDefault: true);
        yield return ApiScope.Create("invoices.write", "Create/Update Invoices", "Billing",
            "Access to create and update invoices");
        yield return ApiScope.Create("payments.read", "Read Payments", "Billing",
            "Access to read payment records", isDefault: true);
        yield return ApiScope.Create("payments.write", "Process Payments", "Billing",
            "Access to process and record payments");
        yield return ApiScope.Create("subscriptions.read", "Read Subscriptions", "Billing",
            "Access to read subscription data", isDefault: true);
        yield return ApiScope.Create("subscriptions.write", "Manage Subscriptions", "Billing",
            "Access to create, update, and cancel subscriptions");

        // Identity - Users (read-only is default)
        yield return ApiScope.Create("users.read", "Read Users", "Identity",
            "Access to read user profiles and data", isDefault: true);
        yield return ApiScope.Create("users.write", "Manage Users", "Identity",
            "Access to create and update users");
        yield return ApiScope.Create("users.manage", "Full User Management", "Identity",
            "Access to create, update, and delete users");

        // Identity - Roles
        yield return ApiScope.Create("roles.read", "Read Roles", "Identity",
            "Access to read roles and role assignments", isDefault: true);
        yield return ApiScope.Create("roles.write", "Create/Update Roles", "Identity",
            "Access to create and update roles");
        yield return ApiScope.Create("roles.manage", "Full Role Management", "Identity",
            "Access to create, update, and delete roles");

        // Identity - Organizations
        yield return ApiScope.Create("organizations.read", "Read Organizations", "Identity",
            "Access to read organization data", isDefault: true);
        yield return ApiScope.Create("organizations.write", "Create/Update Organizations", "Identity",
            "Access to create and update organizations");
        yield return ApiScope.Create("organizations.manage", "Full Organization Management", "Identity",
            "Access to create, update, and delete organizations");

        // Identity - API Keys
        yield return ApiScope.Create("apikeys.read", "Read API Keys", "Identity",
            "Access to read API key metadata", isDefault: true);
        yield return ApiScope.Create("apikeys.write", "Create/Update API Keys", "Identity",
            "Access to create and update API keys");
        yield return ApiScope.Create("apikeys.manage", "Full API Key Management", "Identity",
            "Access to create, update, and revoke API keys");

        // Identity - SSO/SCIM
        yield return ApiScope.Create("sso.read", "Read SSO Configuration", "Identity",
            "Access to read SSO configuration data");
        yield return ApiScope.Create("sso.manage", "Manage SSO Configuration", "Identity",
            "Access to create, update, and delete SSO configurations");
        yield return ApiScope.Create("scim.manage", "Manage SCIM Provisioning", "Identity",
            "Access to manage SCIM directory sync configurations");

        // Identity - Service Accounts
        yield return ApiScope.Create("serviceaccounts.read", "Read Service Accounts", "Identity",
            "Access to read service account data", isDefault: true);
        yield return ApiScope.Create("serviceaccounts.write", "Create/Update Service Accounts", "Identity",
            "Access to create and update service accounts");
        yield return ApiScope.Create("serviceaccounts.manage", "Full Service Account Management", "Identity",
            "Access to create, update, and delete service accounts");

        // Storage scopes
        yield return ApiScope.Create("storage.read", "Read Storage", "Storage",
            "Access to read files and storage data", isDefault: true);
        yield return ApiScope.Create("storage.write", "Write Storage", "Storage",
            "Access to upload and modify files");

        // Communications scopes
        yield return ApiScope.Create("messaging.access", "Access Messaging", "Communications",
            "Access to messaging features");
        yield return ApiScope.Create("announcements.read", "Read Announcements", "Communications",
            "Access to read announcements", isDefault: true);
        yield return ApiScope.Create("announcements.manage", "Manage Announcements", "Communications",
            "Access to create, update, and delete announcements");
        yield return ApiScope.Create("changelog.manage", "Manage Changelog", "Communications",
            "Access to create, update, and delete changelog entries");
        yield return ApiScope.Create("notifications.read", "Read Notifications", "Communications",
            "Access to read notifications");
        yield return ApiScope.Create("notifications.write", "Send Notifications", "Communications",
            "Access to send notifications");

        // Configuration scopes
        yield return ApiScope.Create("configuration.read", "Read Configuration", "Configuration",
            "Access to read configuration data", isDefault: true);
        yield return ApiScope.Create("configuration.manage", "Manage Configuration", "Configuration",
            "Access to create, update, and delete configuration");

        // Showcases scopes
        yield return ApiScope.Create("showcases.read", "Read Showcases", "Showcases",
            "Access to read showcases and showcase data");
        yield return ApiScope.Create("showcases.manage", "Manage Showcases", "Showcases",
            "Access to create, update, and delete showcases");

        // Inquiries scopes
        yield return ApiScope.Create("inquiries.read", "Read Inquiries", "Inquiries",
            "Access to read inquiries and inquiry data");
        yield return ApiScope.Create("inquiries.write", "Create/Update Inquiries", "Inquiries",
            "Access to create and update inquiries");

        // Platform scopes
        yield return ApiScope.Create("webhooks.manage", "Manage Webhook Subscriptions", "Platform",
            "Access to manage webhook subscriptions", isDefault: false);
    }

}

public sealed partial class ApiScopeSeeder
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "All default API scopes already exist")]
    private partial void LogAllScopesExist();

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeding {Count} default API scopes")]
    private partial void LogSeedingScopes(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully seeded {Count} API scopes")]
    private partial void LogScopesSeeded(int count);
}
