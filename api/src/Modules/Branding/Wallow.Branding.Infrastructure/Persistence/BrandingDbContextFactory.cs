using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Branding.Infrastructure.Persistence;

/// <summary>
/// Creates the context for <c>dotnet ef</c> migration commands.
/// </summary>
public class BrandingDbContextFactory : IDesignTimeDbContextFactory<BrandingDbContext>
{
    public BrandingDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<BrandingDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new BrandingDbContext(optionsBuilder.Options);
    }
}
