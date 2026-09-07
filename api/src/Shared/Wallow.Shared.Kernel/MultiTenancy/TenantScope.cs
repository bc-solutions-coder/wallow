using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// Rejects an unset tenant ID before constructing tenant-owned data.
/// </summary>
/// <remarks>
/// Call at construction to prevent unresolved tenant context from creating data under Guid.Empty.
/// </remarks>
public static class TenantScope
{
    /// <summary>
    /// Returns <paramref name="tenantId"/>, or throws if it is the default value.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The tenant id is unset.</exception>
    public static TenantId Require(TenantId tenantId)
    {
        if (tenantId == default)
        {
            throw new ForbiddenAccessException(SharedErrors.TenantRequired);
        }

        return tenantId;
    }
}
