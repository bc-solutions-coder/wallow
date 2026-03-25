using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Billing.Infrastructure.Persistence;

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
        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new BillingDbContext(optionsBuilder.Options);
    }
}
