using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public class AuthAuditDbContextFactory : IDesignTimeDbContextFactory<AuthAuditDbContext>
{
    public AuthAuditDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<AuthAuditDbContext> optionsBuilder = new();

        string password = Environment.GetEnvironmentVariable("WALLOW_DB_PASSWORD") ?? "wallow";
        optionsBuilder.UseNpgsql($"Host=localhost;Database=wallow;Username=wallow;Password={password}");

        return new AuthAuditDbContext(optionsBuilder.Options);
    }
}
