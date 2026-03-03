using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Communications.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly CommunicationsDbContext _context;

    public NotificationRepository(CommunicationsDbContext context)
    {
        _context = context;
    }

    public void Add(Notification notification)
    {
        _context.Notifications.Add(notification);
    }

    public Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default)
    {
        return _context.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Notification>> GetByUserIdPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<Notification> query = _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Notification> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Notification>(items, totalCount, page, pageSize);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
