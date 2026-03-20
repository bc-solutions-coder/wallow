using Wallow.Announcements.Application.Changelogs.Interfaces;
using Wallow.Announcements.Domain.Changelogs.Entities;
using Wallow.Announcements.Domain.Changelogs.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Announcements.Application.Changelogs.Commands.PublishChangelogEntry;

public sealed record PublishChangelogEntryCommand(Guid Id);

public sealed class PublishChangelogEntryHandler(
    IChangelogRepository repository,
    TimeProvider timeProvider)
{
    public async Task<Result> Handle(PublishChangelogEntryCommand command, CancellationToken ct)
    {
        ChangelogEntry? entry = await repository.GetByIdAsync(ChangelogEntryId.Create(command.Id), ct);
        if (entry is null)
        {
            return Result.Failure(Error.NotFound("Changelog", command.Id));
        }

        entry.Publish(timeProvider);
        await repository.UpdateAsync(entry, ct);

        return Result.Success();
    }
}
