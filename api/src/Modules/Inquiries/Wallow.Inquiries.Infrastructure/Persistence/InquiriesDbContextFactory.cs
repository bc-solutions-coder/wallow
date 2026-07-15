using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Inquiries.Infrastructure.Persistence;

public sealed class InquiriesDbContextFactory : IDesignTimeDbContextFactory<InquiriesDbContext>
{
    public InquiriesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<InquiriesDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=wallow;Username=wallow;Password={password}",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries"));

        return new InquiriesDbContext(optionsBuilder.Options);
    }
}
