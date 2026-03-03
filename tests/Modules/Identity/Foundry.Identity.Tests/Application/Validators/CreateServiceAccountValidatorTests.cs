using FluentValidation.TestHelper;
using Foundry.Identity.Application.Commands.CreateServiceAccount;

namespace Foundry.Identity.Tests.Application.Validators;

public class CreateServiceAccountValidatorTests
{
    private static readonly string[] _twoScopes = ["invoices.read", "invoices.write"];
    private static readonly string[] _oneScope = ["scope1"];
    private static readonly string[] _threeScopes = ["scope1", "scope2", "scope3"];

    private readonly CreateServiceAccountValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        // Arrange
        CreateServiceAccountCommand command = new(
            "Test Service Account",
            "Test description",
            _twoScopes);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        // Arrange
        CreateServiceAccountCommand command = new(
            "",
            "Description",
            _oneScope);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Service account name is required");
    }

    [Fact]
    public void Validate_WithNameTooLong_ShouldFail()
    {
        // Arrange
        string longName = new('a', 101);
        CreateServiceAccountCommand command = new(
            longName,
            "Description",
            _oneScope);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must not exceed 100 characters");
    }

    [Fact]
    public void Validate_WithDescriptionTooLong_ShouldFail()
    {
        // Arrange
        string longDescription = new('a', 501);
        CreateServiceAccountCommand command = new(
            "Valid Name",
            longDescription,
            _oneScope);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description must not exceed 500 characters");
    }

    [Fact]
    public void Validate_WithEmptyScopes_ShouldFail()
    {
        // Arrange
        CreateServiceAccountCommand command = new(
            "Valid Name",
            "Description",
            Array.Empty<string>());

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Scopes)
            .WithErrorMessage("At least one scope is required");
    }

    [Fact]
    public void Validate_WithNullDescription_ShouldPass()
    {
        // Arrange
        CreateServiceAccountCommand command = new(
            "Valid Name",
            null,
            _oneScope);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithMaxLengthName_ShouldPass()
    {
        // Arrange
        string maxLengthName = new('a', 100);
        CreateServiceAccountCommand command = new(
            maxLengthName,
            "Description",
            _oneScope);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithMaxLengthDescription_ShouldPass()
    {
        // Arrange
        string maxLengthDescription = new('a', 500);
        CreateServiceAccountCommand command = new(
            "Valid Name",
            maxLengthDescription,
            _oneScope);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithMultipleScopes_ShouldPass()
    {
        // Arrange
        CreateServiceAccountCommand command = new(
            "Valid Name",
            "Description",
            _threeScopes);

        // Act
        TestValidationResult<CreateServiceAccountCommand> result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Scopes);
    }
}
