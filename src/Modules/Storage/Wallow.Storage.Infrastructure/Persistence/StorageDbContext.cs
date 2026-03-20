using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Storage.Infrastructure.Persistence;

public sealed class StorageDbContext : TenantAwareDbContext<StorageDbContext>
{
    public DbSet<StorageBucket> Buckets => Set<StorageBucket>();
    public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<TenantSettingEntity> TenantSettings => Set<TenantSettingEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();

    public StorageDbContext(DbContextOptions<StorageDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("storage");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorageDbContext).Assembly);
        modelBuilder.ApplySettingsConfigurations();

        ApplyTenantQueryFilters(modelBuilder);
    }
}
