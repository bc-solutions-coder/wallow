using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

public class TenantContext : ITenantContext, ITenantContextSetter
{
    public TenantId TenantId { get; internal set; }
    public string TenantName { get; internal set; } = string.Empty;
    public string Region { get; internal set; } = RegionConfiguration.PrimaryRegion;
    public bool IsResolved { get; internal set; }

    public void SetTenant(TenantId tenantId)
    {
        TenantId = tenantId;
        TenantName = string.Empty;
        Region = RegionConfiguration.PrimaryRegion;
        IsResolved = true;
        AmbientTenant.Current = tenantId;
    }

    public void SetTenant(TenantId tenantId, string tenantName, string region = RegionConfiguration.PrimaryRegion)
    {
        TenantId = tenantId;
        TenantName = tenantName;
        Region = region;
        IsResolved = true;
        AmbientTenant.Current = tenantId;
    }

    public void Clear()
    {
        TenantId = default;
        TenantName = string.Empty;
        Region = RegionConfiguration.PrimaryRegion;
        IsResolved = false;
        AmbientTenant.Current = default;
    }
}
