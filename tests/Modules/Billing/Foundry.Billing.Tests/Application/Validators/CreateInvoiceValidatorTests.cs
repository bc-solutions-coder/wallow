using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.CreateInvoice;

namespace Foundry.Billing.Tests.Application.Validators;

public class CreateInvoiceValidatorTests
{
    private readonly CreateInvoiceValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_UserId_Is_Empty()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.Empty,
            "INV-001",
            "USD",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_InvoiceNumber_Is_Empty()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "",
            "USD",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceNumber)
            .WithErrorMessage("Invoice number is required");
    }

    [Fact]
    public void Should_Have_Error_When_InvoiceNumber_Exceeds_MaxLength()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            new string('A', 51),
            "USD",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceNumber)
            .WithErrorMessage("Invoice number must not exceed 50 characters");
    }

    [Fact]
    public void Should_Have_Error_When_Currency_Is_Empty()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            "",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency is required");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U")]
    public void Should_Have_Error_When_Currency_Is_Not_3_Characters(string currency)
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            currency,
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Currency)
            .WithErrorMessage("Currency must be a 3-letter ISO code");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            "USD",
            DateTime.UtcNow.AddDays(30)
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_DueDate_Is_Null()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            "USD",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.DueDate);
    }
}
