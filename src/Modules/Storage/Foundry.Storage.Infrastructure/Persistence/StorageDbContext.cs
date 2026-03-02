using Foundry.Shared.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Storage.Infrastructure.Persistence;

public sealed class StorageDbContext : TenantAwareDbContext<StorageDbContext>
{
    public DbSet<StorageBucket> Buckets => Set<StorageBucket>();
    public DbSet<StoredFile> Files => Set<StoredFile>();

    public StorageDbContext(DbContextOptions<StorageDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("storage");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorageDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
