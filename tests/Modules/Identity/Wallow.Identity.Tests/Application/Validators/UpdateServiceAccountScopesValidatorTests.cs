using FluentValidation.TestHelper;
using Wallow.Identity.Application.Commands.UpdateServiceAccountScopes;
using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Tests.Application.Validators;

public class UpdateServiceAccountScopesValidatorTests
{
    private static readonly string[] _twoScopes = ["invoices.read", "invoices.write"];
    private static readonly string[] _oneScope = ["scope1"];

    private readonly UpdateServiceAccountScopesValidator _validator = new UpdateServiceAccountScopesValidator();

    [Fact]
    public void Validate_WithValidScopes_ShouldPass()
    {
        UpdateServiceAccountScopesCommand command = new(
            ServiceAccountMetadataId.New(), _twoScopes);

        TestValidationResult<UpdateServiceAccountScopesCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyScopes_ShouldFail()
    {
        UpdateServiceAccountScopesCommand command = new(
            ServiceAccountMetadataId.New(),
            []);

        TestValidationResult<UpdateServiceAccountScopesCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Scopes)
            .WithErrorMessage("At least one scope is required");
    }

    [Fact]
    public void Validate_WithSingleScope_ShouldPass()
    {
        UpdateServiceAccountScopesCommand command = new(
            ServiceAccountMetadataId.New(), _oneScope);

        TestValidationResult<UpdateServiceAccountScopesCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Scopes);
    }
}
