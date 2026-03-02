using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class ChangelogRepository : IChangelogRepository
{
    private readonly CommunicationsDbContext _context;

    public ChangelogRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public Task<ChangelogEntry?> GetByIdAsync(ChangelogEntryId id, CancellationToken ct = default)
    {
        return _context.ChangelogEntries
            .AsTracking()
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public Task<ChangelogEntry?> GetByVersionAsync(string version, CancellationToken ct = default)
    {
        return _context.ChangelogEntries
            .AsTracking()
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.Version == version, ct);
    }

    public Task<ChangelogEntry?> GetLatestPublishedAsync(CancellationToken ct = default)
    {
        return _context.ChangelogEntries
            .Include(e => e.Items)
            .Where(e => e.IsPublished)
            .OrderByDescending(e => e.ReleasedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChangelogEntry>> GetPublishedAsync(int limit = 50, CancellationToken ct = default)
    {
        return await _context.ChangelogEntries
            .Include(e => e.Items)
            .AsSplitQuery()
            .Where(e => e.IsPublished)
            .OrderByDescending(e => e.ReleasedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChangelogEntry>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ChangelogEntries
            .Include(e => e.Items)
            .AsSplitQuery()
            .OrderByDescending(e => e.ReleasedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ChangelogEntry entry, CancellationToken ct = default)
    {
        await _context.ChangelogEntries.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ChangelogEntry entry, CancellationToken ct = default)
    {
        _context.ChangelogEntries.Update(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ChangelogEntry entry, CancellationToken ct = default)
    {
        _context.ChangelogEntries.Remove(entry);
        await _context.SaveChangesAsync(ct);
    }
}
