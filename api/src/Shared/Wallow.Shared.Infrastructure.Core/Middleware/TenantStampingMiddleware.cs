using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Shared.Infrastructure.Core.Middleware;

public static class TenantStampingMiddleware
{
    public static void Before(Envelope envelope, ITenantContext tenantContext)
    {
        if (tenantContext.IsResolved)
        {
            envelope.Headers["X-Tenant-Id"] = tenantContext.TenantId.Value.ToString();
            return;
        }

        // Fallback: for bus.InvokeAsync called within an HTTP request, the scoped
        // ITenantContext may not be initialized yet, but the AsyncLocal still carries
        // the tenant from the HTTP middleware pipeline.
        TenantId ambient = AmbientTenant.Current;
        if (ambient != default)
        {
            envelope.Headers["X-Tenant-Id"] = ambient.Value.ToString();
        }
    }
}
