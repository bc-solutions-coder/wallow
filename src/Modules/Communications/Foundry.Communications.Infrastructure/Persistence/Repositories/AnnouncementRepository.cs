using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
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
        return _context.Announcements.FindAsync([id], ct).AsTask();
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

    public async Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
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
