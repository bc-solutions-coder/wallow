using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;

namespace Foundry.Communications.Application.Announcements.Interfaces;

public interface IAnnouncementRepository
{
    Task<Announcement?> GetByIdAsync(AnnouncementId id, CancellationToken ct = default);
    Task<IReadOnlyList<Announcement>> GetPublishedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Announcement announcement, CancellationToken ct = default);
    Task UpdateAsync(Announcement announcement, CancellationToken ct = default);
}
