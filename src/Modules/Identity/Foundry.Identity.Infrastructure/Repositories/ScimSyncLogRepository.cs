using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Identity.Infrastructure.Repositories;

public sealed class ScimSyncLogRepository(IdentityDbContext context) : IScimSyncLogRepository
{

    public async Task<IReadOnlyList<ScimSyncLog>> GetRecentAsync(int limit = 100, CancellationToken ct = default)
    {
        return await context.ScimSyncLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public void Add(ScimSyncLog entity)
    {
        context.ScimSyncLogs.Add(entity);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await context.SaveChangesAsync(ct);
    }
}
