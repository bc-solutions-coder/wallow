using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wallow.Identity.Infrastructure.Modules;

namespace Wallow.Identity.Infrastructure.Persistence;

/// <summary>
/// Creates the context for EF Core migration tooling.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<IdentityDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=wallow;Username=wallow;Password={password}",
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", IdentityModule.Schema));

        IDataProtectionProvider dataProtectionProvider = new EphemeralDataProtectionProvider();

        return new IdentityDbContext(optionsBuilder.Options, dataProtectionProvider);
    }
}
