using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Channels.InApp.Identity;
using Wallow.Shared.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Notifications.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(NotificationsDbContext context) : INotificationRepository
{

    public void Add(Notification notification)
    {
        context.Notifications.Add(notification);
    }

    public Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default)
    {
        return context.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Notification>> GetByUserIdPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;

        IQueryable<Notification> query = context.Notifications
            .Where(n => n.UserId == userId && !n.IsArchived && (n.ExpiresAt == null || n.ExpiresAt > utcNow))
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
        return context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
