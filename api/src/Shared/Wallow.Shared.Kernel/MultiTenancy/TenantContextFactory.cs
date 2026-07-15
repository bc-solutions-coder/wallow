using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

public sealed class TenantContextFactory(ITenantContextSetter tenantContextSetter) : ITenantContextFactory
{
    public IDisposable CreateScope(TenantId tenantId)
    {
        tenantContextSetter.SetTenant(tenantId);
        return new TenantContextScope(tenantContextSetter);
    }

    private sealed class TenantContextScope(ITenantContextSetter setter) : IDisposable
    {
        public void Dispose()
        {
            setter.Clear();
        }
    }
}
