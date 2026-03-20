using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Kernel.MultiTenancy;

public static class TenantQueryExtensions
{
    /// <summary>
    /// Bypasses the tenant global query filter, allowing cross-tenant queries.
    /// Use only for admin/superadmin endpoints that need to see all tenants' data.
    /// </summary>
    public static IQueryable<T> AllTenants<T>(this IQueryable<T> query) where T : class
    {
        return query.IgnoreQueryFilters();
    }
}
