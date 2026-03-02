using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Foundry.Shared.Kernel.MultiTenancy;

public class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantSaveChangesInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        SetTenantId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SetTenantId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void SetTenantId(DbContext? context)
    {
        if (context == null || !_tenantContext.IsResolved)
        {
            return;
        }

        IEnumerable<EntityEntry<ITenantScoped>> entries = context.ChangeTracker
            .Entries<ITenantScoped>()
            .Where(e => e.State == EntityState.Added);

        foreach (EntityEntry<ITenantScoped>? entry in entries)
        {
            entry.Property(nameof(ITenantScoped.TenantId)).CurrentValue = _tenantContext.TenantId;
        }

        IEnumerable<EntityEntry<ITenantScoped>> modified = context.ChangeTracker
            .Entries<ITenantScoped>()
            .Where(e => e.State == EntityState.Modified);

        foreach (EntityEntry<ITenantScoped>? entry in modified)
        {
            if (entry.Property(nameof(ITenantScoped.TenantId)).IsModified)
            {
                entry.Property(nameof(ITenantScoped.TenantId)).IsModified = false;
            }
        }
    }
}
