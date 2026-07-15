using Wallow.Announcements.Domain.Announcements.Entities;
using Wallow.Announcements.Domain.Announcements.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Announcements.Application.Announcements.Interfaces;

public interface IAnnouncementDismissalRepository
{
    Task<IReadOnlyList<AnnouncementDismissal>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task<bool> ExistsAsync(AnnouncementId announcementId, UserId userId, CancellationToken ct = default);
    Task AddAsync(AnnouncementDismissal dismissal, CancellationToken ct = default);
}
