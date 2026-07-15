using Microsoft.EntityFrameworkCore;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.ApiKeys;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.ApiKeys.Infrastructure.Repositories;

public sealed class ApiKeyRepository(ApiKeysDbContext context, TimeProvider timeProvider) : IApiKeyRepository
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

    public Task<ApiKey?> GetByHashAsync(string hash, CancellationToken ct = default)
    {
        return context.ApiKeys
            .AsTracking()
            .FirstOrDefaultAsync(x => x.HashedKey == hash, ct);
    }

    public Task<List<ApiKey>> ListByServiceAccountAsync(string serviceAccountId, Guid tenantId, CancellationToken ct)
    {
        TenantId tid = new(tenantId);
        return context.ApiKeys
            .Where(x => x.ServiceAccountId == serviceAccountId && x.TenantId == tid)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task RevokeAsync(ApiKeyId id, Guid tenantId, Guid revokedBy, CancellationToken ct)
    {
        TenantId tid = new(tenantId);
        ApiKey? key = await context.ApiKeys
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tid, ct);

        if (key is null)
        {
            return;
        }

        key.Revoke(revokedBy, timeProvider);
        await context.SaveChangesAsync(ct);
    }

}
