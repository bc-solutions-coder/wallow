using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Showcases.Infrastructure.Persistence;

public sealed class ShowcasesDbContextFactory : IDesignTimeDbContextFactory<ShowcasesDbContext>
{
    public ShowcasesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ShowcasesDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=wallow;Username=wallow;Password={password}",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "showcases"));

        return new ShowcasesDbContext(optionsBuilder.Options);
    }
}
