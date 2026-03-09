using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Enums;
using Foundry.Communications.Domain.Announcements.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementRepository(CommunicationsDbContext context, TimeProvider timeProvider) : IAnnouncementRepository
{

    public Task<Announcement?> GetByIdAsync(AnnouncementId id, CancellationToken ct = default)
    {
        return context.Announcements
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<IReadOnlyList<Announcement>> GetPublishedAsync(CancellationToken ct = default)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        return await context.Announcements
            .Where(a => a.Status == AnnouncementStatus.Published
                && (a.PublishAt == null || a.PublishAt <= now)
                && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Announcements
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Announcement announcement, CancellationToken ct = default)
    {
        await context.Announcements.AddAsync(announcement, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Announcement announcement, CancellationToken ct = default)
    {
        context.Announcements.Update(announcement);
        await context.SaveChangesAsync(ct);
    }
}
