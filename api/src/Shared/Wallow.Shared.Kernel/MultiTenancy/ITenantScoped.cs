using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

public interface ITenantScoped
{
    TenantId TenantId { get; init; }
}
