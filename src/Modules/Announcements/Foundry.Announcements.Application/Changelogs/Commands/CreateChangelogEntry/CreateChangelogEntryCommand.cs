using Foundry.Announcements.Application.Changelogs.DTOs;
using Foundry.Announcements.Application.Changelogs.Interfaces;
using Foundry.Announcements.Domain.Changelogs.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;

public sealed record CreateChangelogEntryCommand(
    string Version,
    string Title,
    string Content,
    DateTime ReleasedAt);

public sealed class CreateChangelogEntryHandler(
    IChangelogRepository repository,
    TimeProvider timeProvider)
{
    public async Task<Result<ChangelogEntryDto>> Handle(CreateChangelogEntryCommand command, CancellationToken ct)
    {
        ChangelogEntry entry = ChangelogEntry.Create(
            command.Version,
            command.Title,
            command.Content,
            command.ReleasedAt,
            timeProvider);

        await repository.AddAsync(entry, ct);

        return Result.Success(MapToDto(entry));
    }

    private static ChangelogEntryDto MapToDto(ChangelogEntry e) => new(
        e.Id.Value, e.Version, e.Title, e.Content, e.ReleasedAt, e.IsPublished,
        e.Items.Select(i => new ChangelogItemDto(i.Id.Value, i.Description, i.Type)).ToList(),
        e.CreatedAt);
}
