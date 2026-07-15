using FluentValidation;

namespace Wallow.Identity.Application.Commands.UpdateServiceAccountScopes;

public sealed class UpdateServiceAccountScopesValidator : AbstractValidator<UpdateServiceAccountScopesCommand>
{
    public UpdateServiceAccountScopesValidator()
    {
        RuleFor(x => x.Scopes)
            .NotEmpty().WithMessage("At least one scope is required");
    }
}
