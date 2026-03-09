using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementDismissalRepository(CommunicationsDbContext context) : IAnnouncementDismissalRepository
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
