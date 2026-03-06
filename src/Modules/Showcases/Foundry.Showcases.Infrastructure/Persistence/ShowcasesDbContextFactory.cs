using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foundry.Showcases.Infrastructure.Persistence;

public sealed class ShowcasesDbContextFactory : IDesignTimeDbContextFactory<ShowcasesDbContext>
{
    public ShowcasesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ShowcasesDbContext> optionsBuilder = new DbContextOptionsBuilder<ShowcasesDbContext>();

        string password = Environment.GetEnvironmentVariable("FOUNDRY_DB_PASSWORD") ?? "foundry";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=foundry;Username=foundry;Password={password}",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "showcases"));

        return new ShowcasesDbContext(optionsBuilder.Options);
    }
}
