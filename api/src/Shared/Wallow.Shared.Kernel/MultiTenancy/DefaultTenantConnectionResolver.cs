using Microsoft.Extensions.Configuration;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Shared.Kernel.MultiTenancy;

public sealed class DefaultTenantConnectionResolver(IConfiguration configuration) : ITenantConnectionResolver
{
    public Task<string> ResolveConnectionStringAsync(TenantId tenantId, CancellationToken ct)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        return Task.FromResult(connectionString);
    }
}
