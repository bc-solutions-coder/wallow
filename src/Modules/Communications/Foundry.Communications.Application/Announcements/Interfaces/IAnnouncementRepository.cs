using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Pagination;

namespace Foundry.Communications.Application.Announcements.Interfaces;

public interface IAnnouncementRepository
{
    Task<Announcement?> GetByIdAsync(AnnouncementId id, CancellationToken ct = default);
    Task<IReadOnlyList<Announcement>> GetPublishedAsync(CancellationToken ct = default);
    Task<PagedResult<Announcement>> GetPublishedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<Announcement>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Announcement announcement, CancellationToken ct = default);
    Task UpdateAsync(Announcement announcement, CancellationToken ct = default);
    Task DeleteAsync(Announcement announcement, CancellationToken ct = default);
}
