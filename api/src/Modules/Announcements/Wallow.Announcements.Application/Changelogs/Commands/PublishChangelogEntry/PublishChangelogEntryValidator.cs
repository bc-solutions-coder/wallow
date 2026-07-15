using FluentValidation;

namespace Wallow.Announcements.Application.Changelogs.Commands.PublishChangelogEntry;

public sealed class PublishChangelogEntryValidator : AbstractValidator<PublishChangelogEntryCommand>
{
    public PublishChangelogEntryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Changelog entry ID is required");
    }
}
