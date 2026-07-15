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

    [Fact]
    public void Validate_WithWhitespaceOnlyEmail_ShouldFail()
    {
        BootstrapAdminCommand command = new("   ", "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyPassword_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "   ", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyFirstName_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "   ", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyLastName_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "   ");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_WithAllFieldsEmpty_ShouldHaveMultipleErrors()
    {
        BootstrapAdminCommand command = new("", "", "", "");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_WithNullEmail_ShouldFail()
    {
        BootstrapAdminCommand command = new(null!, "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_WithNullPassword_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", null!, "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_WithNullFirstName_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", null!, "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_WithNullLastName_ShouldFail()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", null!);

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("user@domain.com")]
    [InlineData("test.user+tag@sub.domain.org")]
    [InlineData("a@b.co")]
    public void Validate_WithVariousValidEmails_ShouldPass(string email)
    {
        BootstrapAdminCommand command = new(email, "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("missing-at-sign")]
    [InlineData("@no-local-part.com")]
    public void Validate_WithVariousInvalidEmails_ShouldFail(string email)
    {
        BootstrapAdminCommand command = new(email, "P@ssw0rd!", "Admin", "User");

        TestValidationResult<BootstrapAdminCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
