using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.AddLineItem;

namespace Foundry.Billing.Tests.Application.Validators;

public class AddLineItemValidatorTests
{
    private readonly AddLineItemValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_InvoiceId_Is_Empty()
    {
        AddLineItemCommand command = new(
            Guid.Empty,
            "Consulting Services",
            150.00m,
            5,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId)
            .WithErrorMessage("Invoice ID is required");
    }

    [Fact]
    public void Should_Have_Error_When_Description_Is_Empty()
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            "",
            150.00m,
            5,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description is required");
    }

    [Fact]
    public void Should_Have_Error_When_Description_Exceeds_MaxLength()
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            new string('A', 501),
            150.00m,
            5,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description must not exceed 500 characters");
    }

    [Fact]
    public void Should_Have_Error_When_UnitPrice_Is_Negative()
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            "Consulting Services",
            -10.00m,
            5,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UnitPrice)
            .WithErrorMessage("Unit price must be greater than or equal to zero");
    }

    [Fact]
    public void Should_Not_Have_Error_When_UnitPrice_Is_Zero()
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            "Free Item",
            0m,
            1,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Should_Have_Error_When_Quantity_Is_Not_Greater_Than_Zero(int quantity)
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            "Consulting Services",
            150.00m,
            quantity,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Quantity)
            .WithErrorMessage("Quantity must be greater than zero");
    }

    [Fact]
    public void Should_Have_Error_When_UpdatedByUserId_Is_Empty()
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            "Consulting Services",
            150.00m,
            5,
            Guid.Empty
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UpdatedByUserId)
            .WithErrorMessage("Updated by user ID is required");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Command_Is_Valid()
    {
        AddLineItemCommand command = new(
            Guid.NewGuid(),
            "Consulting Services - 10 hours",
            150.00m,
            10,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
