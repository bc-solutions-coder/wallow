using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// Sets tenant context for background work. Create dependent database contexts within
/// the scope; existing contexts retain their own tenant ID.
/// </summary>
public interface ITenantContextFactory
{
    /// <summary>
    /// Creates a scope that sets the tenant context for the given tenant ID.
    /// When disposed, the context is cleared.
    /// </summary>
    IDisposable CreateScope(TenantId tenantId);
}
