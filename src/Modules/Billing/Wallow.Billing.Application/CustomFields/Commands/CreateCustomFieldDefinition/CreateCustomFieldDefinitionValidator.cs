using FluentValidation;

namespace Wallow.Billing.Application.CustomFields.Commands.CreateCustomFieldDefinition;

public sealed class CreateCustomFieldDefinitionValidator : AbstractValidator<CreateCustomFieldDefinitionCommand>
{
    public CreateCustomFieldDefinitionValidator()
    {
        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required")
            .MaximumLength(100).WithMessage("Entity type must not exceed 100 characters");

        RuleFor(x => x.FieldKey)
            .NotEmpty().WithMessage("Field key is required")
            .MaximumLength(50).WithMessage("Field key must not exceed 50 characters")
            .Matches("^[a-z][a-z0-9_]*$")
            .WithMessage("Field key must start with a lowercase letter and contain only lowercase alphanumeric characters and underscores");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters");

        RuleFor(x => x.FieldType)
            .IsInEnum().WithMessage("Field type must be a valid value");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => x.Description is not null);
    }
}
