using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Shared.Infrastructure.Core.Extensions;

public static class TenantAwareDbContextExtensions
{
    /// <summary>
    /// Registers a scoped <typeparamref name="TContext"/> that reads the current tenant from
    /// <see cref="ITenantContext"/> with a fallback to <see cref="AmbientTenant"/> for cases
    /// where the scoped context has not yet been initialized (e.g. Wolverine handler scopes).
    /// </summary>
    public static IServiceCollection AddTenantAwareScopedContext<TContext>(
        this IServiceCollection services)
        where TContext : TenantAwareDbContext<TContext>
    {
        services.AddScoped<TContext>(sp =>
        {
            IDbContextFactory<TContext> factory = sp.GetRequiredService<IDbContextFactory<TContext>>();
            TContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            TenantId tenantId = tenant.IsResolved ? tenant.TenantId : AmbientTenant.Current;
            ctx.SetTenant(tenantId);
            return ctx;
        });

        return services;
    }
}
