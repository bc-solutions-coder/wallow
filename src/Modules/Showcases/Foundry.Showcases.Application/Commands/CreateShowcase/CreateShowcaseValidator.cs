using FluentValidation;

namespace Foundry.Showcases.Application.Commands.CreateShowcase;

public sealed class CreateShowcaseValidator : AbstractValidator<CreateShowcaseCommand>
{
    public CreateShowcaseValidator()
    {
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
