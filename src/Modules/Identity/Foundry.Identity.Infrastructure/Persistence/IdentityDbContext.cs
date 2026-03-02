using Foundry.Identity.Domain.Entities;
using Foundry.Shared.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : TenantAwareDbContext<IdentityDbContext>
{
    public DbSet<ServiceAccountMetadata> ServiceAccountMetadata => Set<ServiceAccountMetadata>();
    public DbSet<ApiScope> ApiScopes => Set<ApiScope>();
    public DbSet<SsoConfiguration> SsoConfigurations => Set<SsoConfiguration>();
    public DbSet<ScimConfiguration> ScimConfigurations => Set<ScimConfiguration>();
    public DbSet<ScimSyncLog> ScimSyncLogs => Set<ScimSyncLog>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
