using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Storage.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for StorageDbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class StorageDbContextFactory : IDesignTimeDbContextFactory<StorageDbContext>
{
    public StorageDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<StorageDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        DesignTimeTenantContext mockTenantContext = new();

        return new StorageDbContext(optionsBuilder.Options, mockTenantContext);
    }
}
