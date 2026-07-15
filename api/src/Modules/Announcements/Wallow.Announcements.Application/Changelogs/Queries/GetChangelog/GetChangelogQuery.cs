using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Application.Changelogs.Mappings;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Changelogs.Queries.GetChangelog;

public sealed record GetChangelogQuery(int Limit = 50);

public sealed class GetChangelogHandler(IChangelogRepository repository)
{

    public async Task<Result<IReadOnlyList<ChangelogEntryDto>>> Handle(GetChangelogQuery query, CancellationToken ct)
    {
        IReadOnlyList<ChangelogEntry> entries = await repository.GetPublishedAsync(query.Limit, ct);
        IReadOnlyList<ChangelogEntryDto> dtos = entries.Select(e => e.ToDto()).ToList();
        return Result.Success(dtos);
    }
}
