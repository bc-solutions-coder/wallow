using FluentValidation;

namespace Foundry.Configuration.Application.Commands;

public sealed class DeactivateCustomFieldDefinitionValidator : AbstractValidator<DeactivateCustomFieldDefinition>
{
    public DeactivateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom field definition ID is required");
    }
}
