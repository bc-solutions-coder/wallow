using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.MultiTenancy;

public sealed class TenantContextFactory : ITenantContextFactory
{
    private readonly ITenantContextSetter _tenantContextSetter;

    public TenantContextFactory(ITenantContextSetter tenantContextSetter)
    {
        _tenantContextSetter = tenantContextSetter;
    }

    public IDisposable CreateScope(TenantId tenantId)
    {
        _tenantContextSetter.SetTenant(tenantId);
        return new TenantContextScope(_tenantContextSetter);
    }

    private sealed class TenantContextScope : IDisposable
    {
        private readonly ITenantContextSetter _setter;

        public TenantContextScope(ITenantContextSetter setter)
        {
            _setter = setter;
        }

        public void Dispose()
        {
            _setter.Clear();
        }
    }
}
