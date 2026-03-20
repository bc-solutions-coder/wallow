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
        }
    }
}
