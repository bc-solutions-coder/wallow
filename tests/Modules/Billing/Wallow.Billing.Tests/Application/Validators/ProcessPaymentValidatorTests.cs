using FluentValidation.TestHelper;
using Wallow.Billing.Application.Commands.ProcessPayment;

namespace Wallow.Billing.Tests.Application.Validators;

public class ProcessPaymentValidatorTests
{
    private readonly ProcessPaymentValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_InvoiceId_Is_Empty()
    {
        ProcessPaymentCommand command = new(
            Guid.Empty,
            Guid.NewGuid(),
            100.00m,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId)
            .WithErrorMessage("Invoice ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        ProcessPaymentCommand command = new(
            Guid.NewGuid(),
            Guid.Empty,
            100.00m,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Should_Have_Error_When_Amount_Is_Not_Greater_Than_Zero(decimal amount)
    {
        ProcessPaymentCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("Amount must be greater than zero");
    }

    [Fact]
    public void Should_Have_Error_When_Currency_Is_Empty()
    {
        ProcessPaymentCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100.00m,
            "",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency is required");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("E")]
    public void Should_Have_Error_When_Currency_Is_Not_3_Characters(string currency)
    {
        ProcessPaymentCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100.00m,
            currency,
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency must be a 3-letter ISO code");
    }

    [Fact]
    public void Should_Have_Error_When_PaymentMethod_Is_Empty()
    {
        ProcessPaymentCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100.00m,
            "USD",
            ""
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod)
            .WithErrorMessage("Payment method is required");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        ProcessPaymentCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            250.75m,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
