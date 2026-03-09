using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foundry.Storage.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for StorageDbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class StorageDbContextFactory : IDesignTimeDbContextFactory<StorageDbContext>
{
    public StorageDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<StorageDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("FOUNDRY_DB_PASSWORD") ?? "foundry";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=foundry;Username=foundry;Password={password}");

        DesignTimeTenantContext mockTenantContext = new();

        return new StorageDbContext(optionsBuilder.Options, mockTenantContext);
    }
}
