using Foundry.Announcements.Domain.Changelogs.Entities;
using Foundry.Announcements.Domain.Changelogs.Identity;

namespace Foundry.Announcements.Application.Changelogs.Interfaces;

public interface IChangelogRepository
{
    Task<ChangelogEntry?> GetByIdAsync(ChangelogEntryId id, CancellationToken ct = default);
    Task<ChangelogEntry?> GetByVersionAsync(string version, CancellationToken ct = default);
    Task<ChangelogEntry?> GetLatestPublishedAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChangelogEntry>> GetPublishedAsync(int limit = 50, CancellationToken ct = default);
    Task AddAsync(ChangelogEntry entry, CancellationToken ct = default);
    Task UpdateAsync(ChangelogEntry entry, CancellationToken ct = default);
}
