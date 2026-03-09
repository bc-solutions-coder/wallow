using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class ChangelogRepository(CommunicationsDbContext context) : IChangelogRepository
{

    public Task<ChangelogEntry?> GetByIdAsync(ChangelogEntryId id, CancellationToken ct = default)
    {
        return context.ChangelogEntries
            .AsTracking()
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public Task<ChangelogEntry?> GetByVersionAsync(string version, CancellationToken ct = default)
    {
        return context.ChangelogEntries
            .AsTracking()
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Version == version, ct);
    }

    public Task<ChangelogEntry?> GetLatestPublishedAsync(CancellationToken ct = default)
    {
        return context.ChangelogEntries
            .Include(e => e.Items)
            .Where(e => e.IsPublished)
            .OrderByDescending(e => e.ReleasedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChangelogEntry>> GetPublishedAsync(int limit = 50, CancellationToken ct = default)
    {
        return await context.ChangelogEntries
            .Include(e => e.Items)
            .AsSplitQuery()
            .Where(e => e.IsPublished)
            .OrderByDescending(e => e.ReleasedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ChangelogEntry entry, CancellationToken ct = default)
    {
        await context.ChangelogEntries.AddAsync(entry, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ChangelogEntry entry, CancellationToken ct = default)
    {
        context.ChangelogEntries.Update(entry);
        await context.SaveChangesAsync(ct);
    }
}
