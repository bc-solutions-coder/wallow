using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// Factory for creating tenant context scopes in background jobs.
/// The created scope sets the TenantContext for the duration of the scope,
/// ensuring all tenant-scoped queries and saves work correctly.
/// </summary>
public interface ITenantContextFactory
{
    /// <summary>
    /// Creates a scope that sets the tenant context for the given tenant ID.
    /// When disposed, the context is cleared.
    /// </summary>
    IDisposable CreateScope(TenantId tenantId);
}
