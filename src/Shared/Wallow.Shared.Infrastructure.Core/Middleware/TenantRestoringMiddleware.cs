using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Shared.Infrastructure.Core.Middleware;

public static class TenantRestoringMiddleware
{
    public static void Before(Envelope envelope, ITenantContextSetter tenantContextSetter)
    {
        if (!envelope.Headers.TryGetValue("X-Tenant-Id", out string? tenantHeader))
        {
            return;
        }

        if (Guid.TryParse(tenantHeader, out Guid tenantGuid))
        {
            tenantContextSetter.SetTenant(TenantId.Create(tenantGuid));
        }
    }
}
