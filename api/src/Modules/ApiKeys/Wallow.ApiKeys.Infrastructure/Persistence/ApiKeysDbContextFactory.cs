using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.ApiKeys.Infrastructure.Persistence;

public class ApiKeysDbContextFactory : IDesignTimeDbContextFactory<ApiKeysDbContext>
{
    public ApiKeysDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ApiKeysDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new ApiKeysDbContext(optionsBuilder.Options);
    }
}
