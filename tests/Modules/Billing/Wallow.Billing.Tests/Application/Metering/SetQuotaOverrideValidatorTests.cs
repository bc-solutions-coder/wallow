using FluentValidation.TestHelper;
using Wallow.Billing.Application.Metering.Commands.SetQuotaOverride;
using Wallow.Billing.Domain.Metering.Enums;

namespace Wallow.Billing.Tests.Application.Metering;

public class SetQuotaOverrideValidatorTests
{
    private readonly SetQuotaOverrideValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_ValidCommand()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "api.calls",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_TenantIdEmpty()
    {
        SetQuotaOverrideCommand command = new(
            Guid.Empty,
            "api.calls",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Should_Have_Error_When_MeterCodeEmpty()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "",
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MeterCode);
    }

    [Fact]
    public void Should_Have_Error_When_MeterCodeTooLong()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            new string('a', 101),
            1000,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MeterCode);
    }

    [Fact]
    public void Should_Have_Error_When_LimitNegative()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "api.calls",
            -1,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Should_Not_Have_Error_When_LimitZero()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "api.calls",
            0,
            QuotaPeriod.Monthly,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void Should_Have_Error_When_PeriodInvalid()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "api.calls",
            1000,
            (QuotaPeriod)99,
            QuotaAction.Block);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Period);
    }

    [Fact]
    public void Should_Have_Error_When_OnExceededInvalid()
    {
        SetQuotaOverrideCommand command = new(
            Guid.NewGuid(),
            "api.calls",
            1000,
            QuotaPeriod.Monthly,
            (QuotaAction)99);

        TestValidationResult<SetQuotaOverrideCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.OnExceeded);
    }
}
