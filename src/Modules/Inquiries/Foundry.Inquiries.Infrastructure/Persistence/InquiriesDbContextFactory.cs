using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foundry.Inquiries.Infrastructure.Persistence;

public sealed class InquiriesDbContextFactory : IDesignTimeDbContextFactory<InquiriesDbContext>
{
    public InquiriesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<InquiriesDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("FOUNDRY_DB_PASSWORD") ?? "foundry";
        optionsBuilder.UseNpgsql(
            $"Host=localhost;Database=foundry;Username=foundry;Password={password}",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries"));

        return new InquiriesDbContext(optionsBuilder.Options);
    }
}
