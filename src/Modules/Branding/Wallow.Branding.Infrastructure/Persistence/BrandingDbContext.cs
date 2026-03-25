using Microsoft.EntityFrameworkCore;
using Wallow.Branding.Domain.Entities;
using Wallow.Shared.Infrastructure.Core.Persistence;

namespace Wallow.Branding.Infrastructure.Persistence;

public sealed class BrandingDbContext : TenantAwareDbContext<BrandingDbContext>
{
    public DbSet<ClientBranding> ClientBrandings => Set<ClientBranding>();

    public BrandingDbContext(DbContextOptions<BrandingDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("branding");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BrandingDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
