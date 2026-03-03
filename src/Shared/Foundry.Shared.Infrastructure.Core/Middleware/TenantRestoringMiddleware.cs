using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Foundry.Shared.Infrastructure.Middleware;

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
