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
}
