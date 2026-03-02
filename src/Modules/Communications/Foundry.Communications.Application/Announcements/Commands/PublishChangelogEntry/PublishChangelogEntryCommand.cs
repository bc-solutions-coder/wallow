using Foundry.Communications.Application.Announcements.Interfaces;
using Foundry.Communications.Domain.Announcements.Entities;
using Foundry.Communications.Domain.Announcements.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Application.Announcements.Commands.PublishChangelogEntry;

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
