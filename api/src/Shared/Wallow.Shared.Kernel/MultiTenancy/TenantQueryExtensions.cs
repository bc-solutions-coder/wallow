using Microsoft.EntityFrameworkCore;

namespace Wallow.Shared.Kernel.MultiTenancy;

public static class TenantQueryExtensions
{
    /// <summary>
    /// Disables all global query filters, including tenant isolation.
    /// Use only where cross-tenant access is explicitly authorized.
    /// </summary>
    public static IQueryable<T> AllTenants<T>(this IQueryable<T> query) where T : class
    {
        return query.IgnoreQueryFilters();
    }
}
