using Wallow.ApiKeys.Domain.Entities;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.ApiKeys.Infrastructure.Persistence;

public sealed class ApiKeysDbContext : TenantAwareDbContext<ApiKeysDbContext>
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public ApiKeysDbContext(DbContextOptions<ApiKeysDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("apikeys");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApiKeysDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
