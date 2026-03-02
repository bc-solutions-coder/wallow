using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.MultiTenancy;

public interface ITenantContext
{
    TenantId TenantId { get; }
    string TenantName { get; }
    string Region { get; }
    bool IsResolved { get; }
}
