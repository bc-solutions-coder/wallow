using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foundry.Inquiries.Infrastructure.Persistence;

public sealed class InquiriesDbContextFactory : IDesignTimeDbContextFactory<InquiriesDbContext>
{
    public InquiriesDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<InquiriesDbContext> optionsBuilder = new DbContextOptionsBuilder<InquiriesDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=foundry;Username=foundry;Password=foundry",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries"));

        return new InquiriesDbContext(optionsBuilder.Options);
    }
}
