using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Announcements.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for AnnouncementsDbContext to enable EF Core migrations.
/// Only used at design-time by dotnet ef commands.
/// </summary>
public class AnnouncementsDbContextFactory : IDesignTimeDbContextFactory<AnnouncementsDbContext>
{
    public AnnouncementsDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<AnnouncementsDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new AnnouncementsDbContext(optionsBuilder.Options);
    }
}
