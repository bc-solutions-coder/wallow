using FluentValidation.TestHelper;
using Wallow.Billing.Application.Metering.Commands.RemoveQuotaOverride;

namespace Wallow.Billing.Tests.Application.Metering;

public class RemoveQuotaOverrideValidatorTests
{
    private readonly RemoveQuotaOverrideValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        RemoveQuotaOverrideCommand command = new(Guid.NewGuid(), "api.calls");

        TestValidationResult<RemoveQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_TenantIdEmpty()
    {
        RemoveQuotaOverrideCommand command = new(Guid.Empty, "api.calls");

        TestValidationResult<RemoveQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Should_Have_Error_When_MeterCodeEmpty()
    {
        RemoveQuotaOverrideCommand command = new(Guid.NewGuid(), "");

        TestValidationResult<RemoveQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MeterCode);
    }

    [Fact]
    public void Should_Have_Error_When_MeterCodeTooLong()
    {
        RemoveQuotaOverrideCommand command = new(Guid.NewGuid(), new string('x', 101));

        TestValidationResult<RemoveQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MeterCode);
    }
}
