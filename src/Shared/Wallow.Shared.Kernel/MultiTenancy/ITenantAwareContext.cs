using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// Implemented by DbContext subclasses that hold a tenant ID set via SetTenant().
/// Used by TenantSaveChangesInterceptor to read the correct tenant for pooled contexts.
/// </summary>
public interface ITenantAwareContext
{
    TenantId CurrentTenantId { get; }
}
