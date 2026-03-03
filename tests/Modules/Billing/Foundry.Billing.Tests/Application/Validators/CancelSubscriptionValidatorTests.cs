using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.CancelSubscription;

namespace Foundry.Billing.Tests.Application.Validators;

public class CancelSubscriptionValidatorTests
{
    private readonly CancelSubscriptionValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_SubscriptionId_Is_Empty()
    {
        CancelSubscriptionCommand command = new CancelSubscriptionCommand(
            Guid.Empty,
            Guid.NewGuid()
        );

        TestValidationResult<CancelSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SubscriptionId)
            .WithErrorMessage("Subscription ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_CancelledByUserId_Is_Empty()
    {
        CancelSubscriptionCommand command = new CancelSubscriptionCommand(
            Guid.NewGuid(),
            Guid.Empty
        );

        TestValidationResult<CancelSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CancelledByUserId)
            .WithErrorMessage("Cancelled by user ID is required");
    }

    [Fact]
    public void Should_Have_Multiple_Errors_When_Both_Ids_Are_Empty()
    {
        CancelSubscriptionCommand command = new CancelSubscriptionCommand(
            Guid.Empty,
            Guid.Empty
        );

        TestValidationResult<CancelSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SubscriptionId);
        result.ShouldHaveValidationErrorFor(x => x.CancelledByUserId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        CancelSubscriptionCommand command = new CancelSubscriptionCommand(
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        TestValidationResult<CancelSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
