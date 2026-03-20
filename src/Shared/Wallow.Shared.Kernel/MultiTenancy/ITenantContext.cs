using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

public interface ITenantContext
{
    TenantId TenantId { get; }
    string TenantName { get; }
    string Region { get; }
    bool IsResolved { get; }
}
