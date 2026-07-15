using Wallow.Announcements.Application.Changelogs.DTOs;
using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Application.Changelogs.Mappings;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;

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

        return Result.Success(entry.ToDto());
    }
}
