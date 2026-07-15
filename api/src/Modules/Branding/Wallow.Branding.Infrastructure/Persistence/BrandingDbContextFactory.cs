using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Branding.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for BrandingDbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class BrandingDbContextFactory : IDesignTimeDbContextFactory<BrandingDbContext>
{
    public BrandingDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<BrandingDbContext> optionsBuilder = new();

        // Use a placeholder connection string for design-time
        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new BrandingDbContext(optionsBuilder.Options);
    }
}
