using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foundry.Billing.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for BillingDbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<BillingDbContext> optionsBuilder = new();

        // Use a placeholder connection string for design-time
        string password = Environment.GetEnvironmentVariable("FOUNDRY_DB_PASSWORD") ?? "postgres";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=foundry;Username=postgres;Password={password}");

        // Create a mock tenant context for design-time
        DesignTimeTenantContext mockTenantContext = new();

        return new BillingDbContext(optionsBuilder.Options, mockTenantContext);
    }
}
