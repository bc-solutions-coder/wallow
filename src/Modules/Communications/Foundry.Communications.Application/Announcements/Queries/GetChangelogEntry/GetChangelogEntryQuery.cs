using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Announcements.Queries.GetChangelogEntry;

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

        return Result.Success(MapToDto(entry));
    }

    private static ChangelogEntryDto MapToDto(ChangelogEntry entry)
    {
        return new ChangelogEntryDto(
            entry.Id.Value,
            entry.Version,
            entry.Title,
            entry.Content,
            entry.ReleasedAt,
            entry.IsPublished,
            entry.Items.Select(i => new ChangelogItemDto(i.Id.Value, i.Description, i.Type)).ToList(),
            entry.CreatedAt);
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

        return Result.Success(MapToDto(entry));
    }

    private static ChangelogEntryDto MapToDto(ChangelogEntry entry)
    {
        return new ChangelogEntryDto(
            entry.Id.Value,
            entry.Version,
            entry.Title,
            entry.Content,
            entry.ReleasedAt,
            entry.IsPublished,
            entry.Items.Select(i => new ChangelogItemDto(i.Id.Value, i.Description, i.Type)).ToList(),
            entry.CreatedAt);
    }
}
