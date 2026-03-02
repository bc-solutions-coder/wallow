using Foundry.Shared.Kernel.Identity;

namespace Foundry.Shared.Kernel.MultiTenancy;

public interface ITenantScoped
{
    TenantId TenantId { get; init; }
}
