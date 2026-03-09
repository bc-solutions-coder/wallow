using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.IssueInvoice;

namespace Foundry.Billing.Tests.Application.Validators;

public class IssueInvoiceValidatorTests
{
    private readonly IssueInvoiceValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_InvoiceId_Is_Empty()
    {
        IssueInvoiceCommand command = new(
            Guid.Empty,
            Guid.NewGuid()
        );

        TestValidationResult<IssueInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId)
            .WithErrorMessage("Invoice ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_IssuedByUserId_Is_Empty()
    {
        IssueInvoiceCommand command = new(
            Guid.NewGuid(),
            Guid.Empty
        );

        TestValidationResult<IssueInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.IssuedByUserId)
            .WithErrorMessage("Issued by user ID is required");
    }

    [Fact]
    public void Should_Have_Multiple_Errors_When_Both_Ids_Are_Empty()
    {
        IssueInvoiceCommand command = new(
            Guid.Empty,
            Guid.Empty
        );

        TestValidationResult<IssueInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId);
        result.ShouldHaveValidationErrorFor(x => x.IssuedByUserId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        IssueInvoiceCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        TestValidationResult<IssueInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
