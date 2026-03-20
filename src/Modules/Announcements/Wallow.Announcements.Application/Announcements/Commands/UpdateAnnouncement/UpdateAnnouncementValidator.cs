using System.Text.RegularExpressions;
using FluentValidation;

namespace Wallow.Announcements.Application.Announcements.Commands.UpdateAnnouncement;

public sealed partial class UpdateAnnouncementValidator : AbstractValidator<UpdateAnnouncementCommand>
{
    public UpdateAnnouncementValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Announcement ID is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required")
            .MaximumLength(5000).WithMessage("Content must not exceed 5000 characters")
            .Must(NotContainScriptTags).WithMessage("Content must not contain script tags");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid announcement type");

        RuleFor(x => x.Target)
            .IsInEnum().WithMessage("Invalid announcement target");

        RuleFor(x => x.ActionUrl)
            .MaximumLength(2000).WithMessage("Action URL must not exceed 2000 characters")
            .When(x => x.ActionUrl is not null);

        RuleFor(x => x.ActionLabel)
            .MaximumLength(100).WithMessage("Action label must not exceed 100 characters")
            .When(x => x.ActionLabel is not null);

        RuleFor(x => x.ImageUrl)
            .MaximumLength(2000).WithMessage("Image URL must not exceed 2000 characters")
            .When(x => x.ImageUrl is not null);
    }

    private static bool NotContainScriptTags(string? content) =>
        content is null || !ScriptTagRegex().IsMatch(content);

    [GeneratedRegex(@"<\s*script", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScriptTagRegex();
}
