using FluentValidation.TestHelper;
using Foundry.Billing.Application.CustomFields.Commands.CreateCustomFieldDefinition;
using Foundry.Shared.Kernel.CustomFields;

namespace Foundry.Billing.Tests.Application.Validators.CustomFields;

public class CreateCustomFieldDefinitionValidatorTests
{
    private readonly CreateCustomFieldDefinitionValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", "PO Number", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_EntityType_Is_Empty()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "", "po_number", "PO Number", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EntityType)
            .WithErrorMessage("Entity type is required");
    }

    [Fact]
    public void Should_Have_Error_When_EntityType_Exceeds_MaxLength()
    {
        CreateCustomFieldDefinitionCommand command = new(
            new string('A', 101), "po_number", "PO Number", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.EntityType)
            .WithErrorMessage("Entity type must not exceed 100 characters");
    }

    [Fact]
    public void Should_Have_Error_When_FieldKey_Is_Empty()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "", "PO Number", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FieldKey)
            .WithErrorMessage("Field key is required");
    }

    [Fact]
    public void Should_Have_Error_When_FieldKey_Exceeds_MaxLength()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", new string('a', 101), "PO Number", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FieldKey)
            .WithErrorMessage("Field key must not exceed 100 characters");
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has-dash")]
    public void Should_Have_Error_When_FieldKey_Has_Invalid_Characters(string fieldKey)
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", fieldKey, "PO Number", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FieldKey)
            .WithErrorMessage("Field key must contain only alphanumeric characters and underscores");
    }

    [Fact]
    public void Should_Have_Error_When_DisplayName_Is_Empty()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", "", CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name is required");
    }

    [Fact]
    public void Should_Have_Error_When_DisplayName_Exceeds_MaxLength()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", new string('A', 201), CustomFieldType.Text);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must not exceed 200 characters");
    }

    [Fact]
    public void Should_Have_Error_When_FieldType_Is_Invalid()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", "PO Number", (CustomFieldType)999);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FieldType)
            .WithErrorMessage("Field type must be a valid value");
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_MaxLength()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", "PO Number", CustomFieldType.Text,
            Description: new string('A', 501));

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description must not exceed 500 characters");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Null()
    {
        CreateCustomFieldDefinitionCommand command = new(
            "Invoice", "po_number", "PO Number", CustomFieldType.Text, Description: null);

        TestValidationResult<CreateCustomFieldDefinitionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }
}
