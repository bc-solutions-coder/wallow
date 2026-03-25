using FluentValidation;

namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

public sealed class BootstrapAdminValidator : AbstractValidator<BootstrapAdminCommand>
{
    public BootstrapAdminValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required");
    }
}
