using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foundry.Configuration.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations.
/// </summary>
public sealed class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ConfigurationDbContext> optionsBuilder = new();

        // Use a dummy connection string for design-time operations
        string password = Environment.GetEnvironmentVariable("FOUNDRY_DB_PASSWORD") ?? "foundry";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=foundry;Username=foundry;Password={password}",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "configuration"));

        // Create a simple tenant context for design time
        DesignTimeTenantContext tenantContext = new();

        return new ConfigurationDbContext(optionsBuilder.Options, tenantContext);
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public TenantId TenantId => TenantId.Create(Guid.Empty);
        public string TenantName => string.Empty;
        public string Region => RegionConfiguration.PrimaryRegion;
        public bool IsResolved => false;
    }
}
