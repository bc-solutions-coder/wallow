using FluentValidation.TestHelper;
using Wallow.Billing.Application.CustomFields.Commands.DeactivateCustomFieldDefinition;
using Wallow.Billing.Application.CustomFields.Commands.ReorderCustomFields;

namespace Wallow.Billing.Tests.Application.Validators.CustomFields;

public class DeactivateCustomFieldDefinitionValidatorTests
{
    private readonly DeactivateCustomFieldDefinitionValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        DeactivateCustomFieldDefinitionCommand command = new(Guid.NewGuid());

        TestValidationResult<DeactivateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        DeactivateCustomFieldDefinitionCommand command = new(Guid.Empty);

        TestValidationResult<DeactivateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("Custom field definition ID is required");
    }
}

public class ReorderCustomFieldsValidatorTests
{
    private readonly ReorderCustomFieldsValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        ReorderCustomFieldsCommand command = new("Invoice", [Guid.NewGuid(), Guid.NewGuid()]);

        TestValidationResult<ReorderCustomFieldsCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_EntityType_Is_Empty()
    {
        ReorderCustomFieldsCommand command = new("", [Guid.NewGuid()]);

        TestValidationResult<ReorderCustomFieldsCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EntityType)
            .WithErrorMessage("Entity type is required");
    }

    [Fact]
    public void Should_Have_Error_When_FieldIdsInOrder_Is_Empty()
    {
        ReorderCustomFieldsCommand command = new("Invoice", []);

        TestValidationResult<ReorderCustomFieldsCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FieldIdsInOrder)
            .WithErrorMessage("Field IDs list must not be empty");
    }
}
