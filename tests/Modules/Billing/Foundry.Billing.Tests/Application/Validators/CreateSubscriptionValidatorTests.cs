using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.CreateSubscription;

namespace Foundry.Billing.Tests.Application.Validators;

public class CreateSubscriptionValidatorTests
{
    private readonly CreateSubscriptionValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        CreateSubscriptionCommand command = new(
            Guid.Empty,
            "Premium Plan",
            29.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_PlanName_Is_Empty()
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "",
            29.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PlanName)
            .WithErrorMessage("Plan name is required");
    }

    [Fact]
    public void Should_Have_Error_When_PlanName_Exceeds_MaxLength()
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            new string('A', 101),
            29.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PlanName)
            .WithErrorMessage("Plan name must not exceed 100 characters");
    }

    [Fact]
    public void Should_Have_Error_When_Price_Is_Negative()
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Premium Plan",
            -10.00m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price must be greater than or equal to zero");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Price_Is_Zero()
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Free Plan",
            0m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Should_Have_Error_When_Currency_Is_Empty()
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Premium Plan",
            29.99m,
            "",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency is required");
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURR")]
    [InlineData("$")]
    public void Should_Have_Error_When_Currency_Is_Not_3_Characters(string currency)
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Premium Plan",
            29.99m,
            currency,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency must be a 3-letter ISO code");
    }

    [Fact]
    public void Should_Have_Error_When_PeriodEnd_Is_Before_StartDate()
    {
        DateTime startDate = DateTime.UtcNow;
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Premium Plan",
            29.99m,
            "USD",
            startDate,
            startDate.AddDays(-1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PeriodEnd)
            .WithErrorMessage("Period end must be after start date");
    }

    [Fact]
    public void Should_Have_Error_When_PeriodEnd_Equals_StartDate()
    {
        DateTime date = DateTime.UtcNow;
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Premium Plan",
            29.99m,
            "USD",
            date,
            date
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PeriodEnd)
            .WithErrorMessage("Period end must be after start date");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        CreateSubscriptionCommand command = new(
            Guid.NewGuid(),
            "Premium Plan",
            29.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
