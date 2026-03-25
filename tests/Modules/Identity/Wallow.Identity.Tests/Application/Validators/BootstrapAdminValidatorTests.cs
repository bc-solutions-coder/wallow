using FluentValidation.TestHelper;
using Wallow.Identity.Application.Commands.BootstrapAdmin;

namespace Wallow.Identity.Tests.Application.Validators;

public class BootstrapAdminValidatorTests
{
    private readonly BootstrapAdminValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyEmail_ShouldFail()
    {
        BootstrapAdminCommand command = new("", "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Fact]
    public void Validate_WithInvalidEmail_ShouldFail()
    {
        BootstrapAdminCommand command = new("not-an-email", "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("A valid email address is required");
    }

    [Fact]
    public void Validate_WithEmptyPassword_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required");
    }

    [Fact]
    public void Validate_WithEmptyFirstName_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FirstName)
            .WithErrorMessage("First name is required");
    }

    [Fact]
    public void Validate_WithEmptyLastName_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.LastName)
            .WithErrorMessage("Last name is required");
    }
}
