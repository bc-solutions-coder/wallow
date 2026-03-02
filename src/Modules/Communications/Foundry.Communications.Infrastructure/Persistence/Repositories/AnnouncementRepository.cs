using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementRepository : IAnnouncementRepository
{
    private readonly CommunicationsDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AnnouncementRepository(CommunicationsDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public Task<Announcement?> GetByIdAsync(AnnouncementId id, CancellationToken ct = default)
    {
        return _context.Announcements
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<Announcement>> GetPublishedAsync(CancellationToken ct = default)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        return await _context.Announcements
            .Where(a => a.Status == AnnouncementStatus.Published
                && (a.PublishAt == null || a.PublishAt <= now)
                && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishAt)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<Announcement>> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        IQueryable<Announcement> query = _context.Announcements
            .Where(a => a.Status == AnnouncementStatus.Published
                && (a.PublishAt == null || a.PublishAt <= now)
                && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishAt);

        int totalCount = await query.CountAsync(ct);
        IReadOnlyList<Announcement> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Announcement>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<Announcement>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Announcement> query = _context.Announcements
            .OrderByDescending(a => a.CreatedAt);

        int totalCount = await query.CountAsync(ct);
        IReadOnlyList<Announcement> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Announcement>(items, totalCount, page, pageSize);
    }

    public async Task AddAsync(Announcement announcement, CancellationToken ct = default)
    {
        await _context.Announcements.AddAsync(announcement, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Announcement announcement, CancellationToken ct = default)
    {
        _context.Announcements.Update(announcement);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Announcement announcement, CancellationToken ct = default)
    {
        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync(ct);
    }
}
