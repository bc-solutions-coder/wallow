using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.MultiTenancy;

public interface ITenantContextSetter
{
    void SetTenant(TenantId tenantId);
    void SetTenant(TenantId tenantId, string tenantName, string region = RegionConfiguration.PrimaryRegion);
    string Region { get; }
    void Clear();
}
