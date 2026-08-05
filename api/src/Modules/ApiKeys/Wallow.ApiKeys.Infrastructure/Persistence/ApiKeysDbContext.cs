using Microsoft.EntityFrameworkCore;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Modules;
using Wallow.Shared.Infrastructure.Core.Persistence;

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
        modelBuilder.HasDefaultSchema(ApiKeysModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApiKeysDbContext).Assembly);

        ApplyTenantQueryFilters(modelBuilder);
    }
}
