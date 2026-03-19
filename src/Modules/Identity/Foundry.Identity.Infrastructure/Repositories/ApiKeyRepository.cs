using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

public sealed class ApiKeyRepository(IdentityDbContext context) : IApiKeyRepository
{
    public async Task AddAsync(ApiKey key, CancellationToken ct)
    {
        context.ApiKeys.Add(key);
        await context.SaveChangesAsync(ct);
    }

    public Task<ApiKey?> GetByHashAsync(string hash, Guid tenantId, CancellationToken ct)
    {
        TenantId tid = new(tenantId);
        return context.ApiKeys
            .AsTracking()
            .FirstOrDefaultAsync(x => x.HashedKey == hash && x.TenantId == tid, ct);
    }

    public Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct)
    {
        TenantId tid = new(tenantId);
        return context.ApiKeys
            .Where(x => x.ServiceAccountId == serviceAccountId && x.TenantId == tid)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task RevokeAsync(ApiKeyId id, Guid tenantId, CancellationToken ct)
    {
        TenantId tid = new(tenantId);
        await context.ApiKeys
            .Where(x => x.Id == id && x.TenantId == tid)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.IsRevoked, true), ct);
    }

    public Task<ApiKey?> GetByIdAsync(ApiKeyId id, Guid tenantId, CancellationToken ct)
    {
        TenantId tid = new(tenantId);
        return context.ApiKeys
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid, ct);
    }
}
