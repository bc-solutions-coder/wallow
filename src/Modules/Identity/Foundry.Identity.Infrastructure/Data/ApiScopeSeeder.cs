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

        // Identity scopes (read-only is default)
        yield return ApiScope.Create("users.read", "Read Users", "Identity",
            "Access to read user profiles and data", isDefault: true);
        yield return ApiScope.Create("users.write", "Manage Users", "Identity",
            "Access to create and update users");

        // Notifications scopes
        yield return ApiScope.Create("notifications.read", "Read Notifications", "Notifications",
            "Access to read notifications");
        yield return ApiScope.Create("notifications.write", "Send Notifications", "Notifications",
            "Access to send notifications");

        // Showcases scopes
        yield return ApiScope.Create("showcases.read", "Read Showcases", "Showcases",
            "Access to read showcases and showcase data");

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
