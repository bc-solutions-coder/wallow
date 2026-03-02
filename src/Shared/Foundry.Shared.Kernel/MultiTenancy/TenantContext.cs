using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.MultiTenancy;

public class TenantContext : ITenantContext, ITenantContextSetter
{
    public TenantId TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Region { get; set; } = RegionConfiguration.PrimaryRegion;
    public bool IsResolved { get; set; }

    public void SetTenant(TenantId tenantId)
    {
        TenantId = tenantId;
        TenantName = string.Empty;
        Region = RegionConfiguration.PrimaryRegion;
        IsResolved = true;
    }

    public void SetTenant(TenantId tenantId, string tenantName, string region = RegionConfiguration.PrimaryRegion)
    {
        TenantId = tenantId;
        TenantName = tenantName;
        Region = region;
        IsResolved = true;
    }

    public void Clear()
    {
        TenantId = default;
        TenantName = string.Empty;
        Region = RegionConfiguration.PrimaryRegion;
        IsResolved = false;
    }
}
