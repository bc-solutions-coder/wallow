using FluentValidation;

namespace Foundry.Billing.Application.CustomFields.Commands.UpdateCustomFieldDefinition;

public sealed class UpdateCustomFieldDefinitionValidator : AbstractValidator<UpdateCustomFieldDefinitionCommand>
{
    public UpdateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom field definition ID is required");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name must not be empty when provided")
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters")
            .When(x => x.DisplayName is not null);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be zero or greater")
            .When(x => x.DisplayOrder.HasValue);
    }
}
