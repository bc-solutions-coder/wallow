using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

/// <summary>
/// The check every tenant-scoped row passes before it exists.
/// </summary>
/// <remarks>
/// A request that resolved no tenant leaves <see cref="ITenantContext.TenantId"/> at its default,
/// and a row built from it lands in the empty tenant: matched by no query filter, owned by nobody,
/// and invisible to the organization it was meant for. Refusing at construction turns that into a
/// failed request instead of an orphan row.
/// </remarks>
public static class TenantScope
{
    /// <summary>
    /// Returns <paramref name="tenantId"/>, or throws if it is the default value.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The tenant id is unset.</exception>
    public static TenantId Require(TenantId tenantId, string entityName)
    {
        if (tenantId == default)
        {
            throw new ForbiddenAccessException(
                SharedErrors.Forbidden,
                $"Cannot create {entityName} without a resolved tenant");
        }

        return tenantId;
    }
}
