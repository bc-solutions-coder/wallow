using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Notifications.Domain.Channels.InApp.Identity;
using Wallow.Shared.Kernel.Pagination;

namespace Wallow.Notifications.Application.Channels.InApp.Interfaces;

public interface INotificationRepository
{
    void Add(Notification notification);
    Task<Notification?> GetByIdAsync(NotificationId id, CancellationToken cancellationToken = default);
    Task<PagedResult<Notification>> GetByUserIdPagedAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
