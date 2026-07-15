using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Application.Changelogs.Mappings;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Changelogs.Queries.GetChangelogEntry;

public sealed record GetChangelogByVersionQuery(string Version);
public sealed record GetLatestChangelogQuery;

public sealed class GetChangelogByVersionHandler(IChangelogRepository repository)
{

    public async Task<Result<ChangelogEntryDto>> Handle(GetChangelogByVersionQuery query, CancellationToken ct)
    {
        ChangelogEntry? entry = await repository.GetByVersionAsync(query.Version, ct);

        if (entry is null || !entry.IsPublished)
        {
            return Result.Failure<ChangelogEntryDto>(Error.NotFound("Changelog", query.Version));
        }

        return Result.Success(entry.ToDto());
    }
}

public sealed class GetLatestChangelogHandler(IChangelogRepository repository)
{

    public async Task<Result<ChangelogEntryDto>> Handle(GetLatestChangelogQuery _, CancellationToken ct)
    {
        ChangelogEntry? entry = await repository.GetLatestPublishedAsync(ct);

        if (entry is null)
        {
            return Result.Failure<ChangelogEntryDto>(Error.NotFound("Changelog", "latest"));
        }

        return Result.Success(entry.ToDto());
    }
}
