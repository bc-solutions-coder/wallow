using Foundry.Communications.Application.Announcements.DTOs;
using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Announcements.Queries.GetChangelog;

public sealed record GetChangelogQuery(int Limit = 50);

public sealed class GetChangelogHandler(IChangelogRepository repository)
{

    public async Task<Result<IReadOnlyList<ChangelogEntryDto>>> Handle(GetChangelogQuery query, CancellationToken ct)
    {
        IReadOnlyList<ChangelogEntry> entries = await repository.GetPublishedAsync(query.Limit, ct);
        IReadOnlyList<ChangelogEntryDto> dtos = entries.Select(MapToDto).ToList();
        return Result.Success(dtos);
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
