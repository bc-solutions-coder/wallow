using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.CancelInvoice;

namespace Foundry.Billing.Tests.Application.Validators;

public class CancelInvoiceValidatorTests
{
    private readonly CancelInvoiceValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_InvoiceId_Is_Empty()
    {
        CancelInvoiceCommand command = new(
            Guid.Empty,
            Guid.NewGuid()
        );

        TestValidationResult<CancelInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId)
            .WithErrorMessage("Invoice ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_CancelledByUserId_Is_Empty()
    {
        CancelInvoiceCommand command = new(
            Guid.NewGuid(),
            Guid.Empty
        );

        TestValidationResult<CancelInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CancelledByUserId)
            .WithErrorMessage("Cancelled by user ID is required");
    }

    [Fact]
    public void Should_Have_Multiple_Errors_When_Both_Ids_Are_Empty()
    {
        CancelInvoiceCommand command = new(
            Guid.Empty,
            Guid.Empty
        );

        TestValidationResult<CancelInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId);
        result.ShouldHaveValidationErrorFor(x => x.CancelledByUserId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        CancelInvoiceCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        TestValidationResult<CancelInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
