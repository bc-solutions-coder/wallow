using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Infrastructure.Persistence;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Configuration.Infrastructure.Persistence;

public sealed class ConfigurationDbContext : TenantAwareDbContext<ConfigurationDbContext>
{
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<FeatureFlagOverride> FeatureFlagOverrides => Set<FeatureFlagOverride>();

    public ConfigurationDbContext(
        DbContextOptions<ConfigurationDbContext> options,
        ITenantContext tenantContext) : base(options, tenantContext)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("configuration");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConfigurationDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
