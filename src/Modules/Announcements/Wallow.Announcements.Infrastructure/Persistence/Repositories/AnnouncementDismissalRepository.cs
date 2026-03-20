using Wallow.Announcements.Application.Announcements.Interfaces;
using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Announcements.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementDismissalRepository(AnnouncementsDbContext context) : IAnnouncementDismissalRepository
{

    public async Task<IReadOnlyList<AnnouncementDismissal>> GetByUserIdAsync(UserId userId, CancellationToken ct = default)
    {
        return await context.AnnouncementDismissals
            .Where(d => d.UserId == userId)
            .ToListAsync(ct);
    }

    public Task<bool> ExistsAsync(AnnouncementId announcementId, UserId userId, CancellationToken ct = default)
    {
        return context.AnnouncementDismissals
            .AnyAsync(d => d.AnnouncementId == announcementId && d.UserId == userId, ct);
    }

    public async Task AddAsync(AnnouncementDismissal dismissal, CancellationToken ct = default)
    {
        await context.AnnouncementDismissals.AddAsync(dismissal, ct);
        await context.SaveChangesAsync(ct);
    }
}
