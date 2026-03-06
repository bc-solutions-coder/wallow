using FluentValidation;

namespace Foundry.Showcases.Application.Commands.UpdateShowcase;

public sealed class UpdateShowcaseValidator : AbstractValidator<UpdateShowcaseCommand>
{
    public UpdateShowcaseValidator()
    {
        RuleFor(x => x.ShowcaseId)
            .Must(id => id.Value != Guid.Empty).WithMessage("ShowcaseId is required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.DemoUrl)
                     || !string.IsNullOrWhiteSpace(x.GitHubUrl)
                     || !string.IsNullOrWhiteSpace(x.VideoUrl))
            .WithMessage("At least one of DemoUrl, GitHubUrl, or VideoUrl must be provided");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Category must be a valid value");
    }
}
