using Foundry.Notifications.Domain.Channels.InApp.Entities;
using Foundry.Notifications.Domain.Channels.InApp.Identity;
using Foundry.Shared.Kernel.Pagination;

namespace Foundry.Notifications.Application.Channels.InApp.Interfaces;

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
