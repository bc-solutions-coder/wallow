using FluentValidation;

namespace Foundry.Configuration.Application.Commands;

public sealed class ReorderCustomFieldsValidator : AbstractValidator<ReorderCustomFields>
{
    public ReorderCustomFieldsValidator()
    {
        RuleFor(x => x.FieldIdsInOrder)
            .NotNull().WithMessage("Field IDs list is required")
            .NotEmpty().WithMessage("Field IDs list must not be empty");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required");
    }
}
