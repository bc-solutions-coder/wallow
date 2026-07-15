using FluentValidation.TestHelper;
using Wallow.Identity.Application.Commands.RegisterSetupClient;

namespace Wallow.Identity.Tests.Application.Validators;

public class RegisterSetupClientValidatorTests
{
    private readonly RegisterSetupClientValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyClientId_ShouldFail()
    {
        RegisterSetupClientCommand command = new("", ["https://localhost/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ClientId)
            .WithErrorMessage("Client ID is required");
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyClientId_ShouldFail()
    {
        RegisterSetupClientCommand command = new("   ", ["https://localhost/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void Validate_WithEmptyRedirectUris_ShouldPass()
    {
        RegisterSetupClientCommand command = new("setup-client", []);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMultipleRedirectUris_ShouldPass()
    {
        RegisterSetupClientCommand command = new("setup-client", ["https://localhost/callback", "https://app.example.com/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithNullClientId_ShouldFail()
    {
        RegisterSetupClientCommand command = new(null!, ["https://localhost/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void Validate_WithTabOnlyClientId_ShouldFail()
    {
        RegisterSetupClientCommand command = new("\t", ["https://localhost/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Theory]
    [InlineData("my-client")]
    [InlineData("client-123")]
    [InlineData("a")]
    public void Validate_WithVariousValidClientIds_ShouldPass(string clientId)
    {
        RegisterSetupClientCommand command = new(clientId, ["https://localhost/callback"]);

        TestValidationResult<RegisterSetupClientCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
