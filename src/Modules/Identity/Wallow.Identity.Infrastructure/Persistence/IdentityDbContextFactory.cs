using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for IdentityDbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<IdentityDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=wallow;Username=wallow;Password={password}",
            npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity"));

        IDataProtectionProvider dataProtectionProvider = new EphemeralDataProtectionProvider();

        return new IdentityDbContext(optionsBuilder.Options, dataProtectionProvider);
    }
}
