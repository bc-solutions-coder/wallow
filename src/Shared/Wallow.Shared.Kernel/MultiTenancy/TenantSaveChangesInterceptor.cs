using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wallow.Shared.Kernel.MultiTenancy;

public class TenantSaveChangesInterceptor(ITenantContext? tenantContext = null) : SaveChangesInterceptor
{

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
        if (context == null)
        {
            return;
        }

        // Prefer the tenant ID set directly on the context (via SetTenant) over the injected
        // ITenantContext, because with pooled DbContextFactory the interceptor may capture a
        // stale ITenantContext from a different scope.
        TenantId effectiveTenantId = ResolveTenantId(context);
        if (effectiveTenantId == default)
        {
            return;
        }

        IEnumerable<EntityEntry<ITenantScoped>> entries = context.ChangeTracker
            .Entries<ITenantScoped>()
            .Where(e => e.State == EntityState.Added);

        foreach (EntityEntry<ITenantScoped> entry in entries)
        {
            entry.Property(nameof(ITenantScoped.TenantId)).CurrentValue = effectiveTenantId;
        }

        IEnumerable<EntityEntry<ITenantScoped>> modified = context.ChangeTracker
            .Entries<ITenantScoped>()
            .Where(e => e.State == EntityState.Modified);

        foreach (EntityEntry<ITenantScoped> entry in modified)
        {
            if (entry.Property(nameof(ITenantScoped.TenantId)).IsModified)
            {
                entry.Property(nameof(ITenantScoped.TenantId)).IsModified = false;
            }
        }
    }

    private TenantId ResolveTenantId(DbContext context)
    {
        // ITenantAwareContext is implemented by TenantAwareDbContext which exposes CurrentTenantId
        if (context is ITenantAwareContext tenantAware && tenantAware.CurrentTenantId != default)
        {
            return tenantAware.CurrentTenantId;
        }

        if (tenantContext is { IsResolved: true })
        {
            return tenantContext.TenantId;
        }

        return default;
    }
}
