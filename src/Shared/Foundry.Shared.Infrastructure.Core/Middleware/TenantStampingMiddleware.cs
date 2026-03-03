using Foundry.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Middleware;

public static class TenantStampingMiddleware
{
    public static void Before(Envelope envelope, ITenantContext tenantContext)
    {
        if (tenantContext.IsResolved)
        {
            envelope.Headers["X-Tenant-Id"] = tenantContext.TenantId.Value.ToString();
        }
    }
}
