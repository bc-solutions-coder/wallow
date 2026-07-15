using FluentValidation;

namespace Wallow.Identity.Application.Commands.RegisterSetupClient;

public sealed class RegisterSetupClientValidator : AbstractValidator<RegisterSetupClientCommand>
{
    public RegisterSetupClientValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("Client ID is required");
    }
}
