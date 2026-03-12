using System.Text.RegularExpressions;
using FluentValidation;

namespace Foundry.Announcements.Application.Changelogs.Commands.CreateChangelogEntry;

public sealed partial class CreateChangelogEntryValidator : AbstractValidator<CreateChangelogEntryCommand>
{
    public CreateChangelogEntryValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required")
            .MaximumLength(10000).WithMessage("Content must not exceed 10000 characters");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required")
            .Matches(SemVerRegex()).WithMessage("Version must be in valid semver format (e.g. 1.0.0)");
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[\w.]+)?(\+[\w.]+)?$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SemVerRegex();
}
