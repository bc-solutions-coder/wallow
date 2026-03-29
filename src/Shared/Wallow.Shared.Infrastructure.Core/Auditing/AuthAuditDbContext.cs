using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Infrastructure.Core.Auditing;

public sealed class AuthAuditDbContext(DbContextOptions<AuthAuditDbContext> options) : DbContext(options)
{
    public DbSet<AuthAuditEntry> AuthAuditEntries => Set<AuthAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth_audit");

        modelBuilder.Entity<AuthAuditEntry>(entity =>
        {
            entity.ToTable("auth_audit_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("now()");
        });
    }
}
