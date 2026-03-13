using FluentValidation.TestHelper;
using Foundry.Billing.Application.CustomFields.Commands.UpdateCustomFieldDefinition;

namespace Foundry.Billing.Tests.Application.Validators.CustomFields;

public class UpdateCustomFieldDefinitionValidatorTests
{
    private readonly UpdateCustomFieldDefinitionValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), DisplayName: "New Name");

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.Empty);

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("Custom field definition ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_DisplayName_Is_Empty_String()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), DisplayName: "");

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must not be empty when provided");
    }

    [Fact]
    public void Should_Have_Error_When_DisplayName_Exceeds_MaxLength()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), DisplayName: new string('A', 201));

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must not exceed 200 characters");
    }

    [Fact]
    public void Should_Not_Have_Error_When_DisplayName_Is_Null()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), DisplayName: null);

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_MaxLength()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), Description: new string('A', 501));

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description must not exceed 500 characters");
    }

    [Fact]
    public void Should_Have_Error_When_DisplayOrder_Is_Negative()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), DisplayOrder: -1);

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayOrder)
            .WithErrorMessage("Display order must be zero or greater");
    }

    [Fact]
    public void Should_Not_Have_Error_When_DisplayOrder_Is_Zero()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid(), DisplayOrder: 0);

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.DisplayOrder);
    }

    [Fact]
    public void Should_Not_Have_Error_When_DisplayOrder_Is_Null()
    {
        UpdateCustomFieldDefinitionCommand command = new(Guid.NewGuid());

        TestValidationResult<UpdateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.DisplayOrder);
    }
}
