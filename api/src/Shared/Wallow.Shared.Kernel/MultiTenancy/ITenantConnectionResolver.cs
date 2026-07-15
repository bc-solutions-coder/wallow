using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

// TODO: The default registration returns a single connection string for all tenants.
// Future strategies can replace this registration to route tenants to separate database instances
// (e.g., per-tenant connection strings from a catalog database or configuration store).
public interface ITenantConnectionResolver
{
    Task<string> ResolveConnectionStringAsync(TenantId tenantId, CancellationToken ct);
}
