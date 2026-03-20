using FluentValidation;

namespace Wallow.Billing.Application.CustomFields.Commands.DeactivateCustomFieldDefinition;

public sealed class DeactivateCustomFieldDefinitionValidator : AbstractValidator<DeactivateCustomFieldDefinitionCommand>
{
    public DeactivateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom field definition ID is required");
    }
}
