using FluentValidation;

namespace Wallow.Identity.Application.Commands.CreateServiceAccount;

public sealed class CreateServiceAccountValidator : AbstractValidator<CreateServiceAccountCommand>
{
    public CreateServiceAccountValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Service account name is required")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");

        RuleFor(x => x.Scopes)
            .NotEmpty().WithMessage("At least one scope is required");
    }
}
