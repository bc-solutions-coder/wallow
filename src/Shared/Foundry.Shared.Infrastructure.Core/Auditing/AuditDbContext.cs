using Microsoft.EntityFrameworkCore;

namespace Foundry.Shared.Infrastructure.Auditing;

public sealed class AuditDbContext : DbContext
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
        });
    }
}
