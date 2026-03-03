using FluentValidation.TestHelper;
using Foundry.Billing.Application.Commands.AddLineItem;
using Foundry.Billing.Application.Commands.CancelInvoice;
using Foundry.Billing.Application.Commands.CancelSubscription;
using Foundry.Billing.Application.Commands.CreateInvoice;
using Foundry.Billing.Application.Commands.CreateSubscription;
using Foundry.Billing.Application.Commands.IssueInvoice;
using Foundry.Billing.Application.Commands.ProcessPayment;

namespace Foundry.Billing.Tests.Application.Validators;

// --- CreateInvoiceValidator ---

public class CreateInvoiceValidatorBoundaryTests
{
    private readonly CreateInvoiceValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_InvoiceNumber_Is_Exactly_50_Characters()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            new string('A', 50),
            "USD",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.InvoiceNumber);
    }

    [Fact]
    public void Should_Have_Error_When_InvoiceNumber_Is_Whitespace()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "   ",
            "USD",
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceNumber);
    }

    [Fact]
    public void Should_Not_Have_Error_When_DueDate_Is_In_The_Past()
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            "USD",
            DateTime.UtcNow.AddDays(-30)
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void Should_Not_Have_Error_When_Currency_Is_Valid_3_Letter_Code(string currency)
    {
        CreateInvoiceCommand command = new CreateInvoiceCommand(
            Guid.NewGuid(),
            "INV-001",
            currency,
            null
        );

        TestValidationResult<CreateInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }
}

// --- ProcessPaymentValidator ---

public class ProcessPaymentValidatorBoundaryTests
{
    private readonly ProcessPaymentValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Amount_Is_Minimal_Positive_Value()
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0.01m,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Amount_Is_Exactly_Zero()
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            0m,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("Amount must be greater than zero");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Amount_Is_Very_Large()
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            999_999_999.99m,
            "USD",
            "CreditCard"
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_PaymentMethod_Is_Whitespace()
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100.00m,
            "USD",
            "   "
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    public void Should_Not_Have_Error_When_CustomFields_Is_Null()
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50.00m,
            "EUR",
            "BankTransfer",
            null
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_CustomFields_Is_Provided()
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            50.00m,
            "EUR",
            "BankTransfer",
            new Dictionary<string, object> { { "reference", "TX-12345" } }
        );

        TestValidationResult<ProcessPaymentCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-999.99)]
    public void Should_Have_Error_When_Amount_Is_Negative(decimal amount)
    {
        ProcessPaymentCommand command = new ProcessPaymentCommand(
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
}

// --- AddLineItemValidator ---

public class AddLineItemValidatorBoundaryTests
{
    private readonly AddLineItemValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Description_Is_Exactly_500_Characters()
    {
        AddLineItemCommand command = new AddLineItemCommand(
            Guid.NewGuid(),
            new string('A', 500),
            150.00m,
            1,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Should_Have_Error_When_Quantity_Is_Exactly_Zero()
    {
        AddLineItemCommand command = new AddLineItemCommand(
            Guid.NewGuid(),
            "Service",
            100.00m,
            0,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Quantity)
            .WithErrorMessage("Quantity must be greater than zero");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Quantity_Is_One()
    {
        AddLineItemCommand command = new AddLineItemCommand(
            Guid.NewGuid(),
            "Service",
            100.00m,
            1,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void Should_Not_Have_Error_When_UnitPrice_Is_Exactly_Zero()
    {
        AddLineItemCommand command = new AddLineItemCommand(
            Guid.NewGuid(),
            "Free Consultation",
            0m,
            1,
            Guid.NewGuid()
        );

        TestValidationResult<AddLineItemCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.UnitPrice);
    }
}

// --- CreateSubscriptionValidator ---

public class CreateSubscriptionValidatorBoundaryTests
{
    private readonly CreateSubscriptionValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_PlanName_Is_Exactly_100_Characters()
    {
        CreateSubscriptionCommand command = new CreateSubscriptionCommand(
            Guid.NewGuid(),
            new string('P', 100),
            29.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.PlanName);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Price_Is_Exactly_Zero()
    {
        CreateSubscriptionCommand command = new CreateSubscriptionCommand(
            Guid.NewGuid(),
            "Free Plan",
            0m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100.00)]
    public void Should_Have_Error_When_Price_Is_Negative(decimal price)
    {
        CreateSubscriptionCommand command = new CreateSubscriptionCommand(
            Guid.NewGuid(),
            "Plan",
            price,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price must be greater than or equal to zero");
    }

    [Fact]
    public void Should_Not_Have_Error_When_PeriodEnd_Is_Far_In_The_Future()
    {
        CreateSubscriptionCommand command = new CreateSubscriptionCommand(
            Guid.NewGuid(),
            "Annual Plan",
            99.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddYears(5)
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_CustomFields_Is_Provided()
    {
        CreateSubscriptionCommand command = new CreateSubscriptionCommand(
            Guid.NewGuid(),
            "Premium Plan",
            29.99m,
            "USD",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            new Dictionary<string, object> { { "tier", "gold" } }
        );

        TestValidationResult<CreateSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

// --- CancelInvoiceValidator ---

public class CancelInvoiceValidatorBoundaryTests
{
    private readonly CancelInvoiceValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Both_Ids_Are_Valid()
    {
        CancelInvoiceCommand command = new CancelInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        TestValidationResult<CancelInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_InvoiceId_Is_Default()
    {
        CancelInvoiceCommand command = new CancelInvoiceCommand(
            default,
            Guid.NewGuid()
        );

        TestValidationResult<CancelInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.InvoiceId)
            .WithErrorMessage("Invoice ID is required");
    }
}

// --- IssueInvoiceValidator ---

public class IssueInvoiceValidatorBoundaryTests
{
    private readonly IssueInvoiceValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Both_Ids_Are_Valid()
    {
        IssueInvoiceCommand command = new IssueInvoiceCommand(
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        TestValidationResult<IssueInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_IssuedByUserId_Is_Default()
    {
        IssueInvoiceCommand command = new IssueInvoiceCommand(
            Guid.NewGuid(),
            default
        );

        TestValidationResult<IssueInvoiceCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.IssuedByUserId)
            .WithErrorMessage("Issued by user ID is required");
    }
}

// --- CancelSubscriptionValidator ---

public class CancelSubscriptionValidatorBoundaryTests
{
    private readonly CancelSubscriptionValidator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Both_Ids_Are_Valid()
    {
        CancelSubscriptionCommand command = new CancelSubscriptionCommand(
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        TestValidationResult<CancelSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_SubscriptionId_Is_Default()
    {
        CancelSubscriptionCommand command = new CancelSubscriptionCommand(
            default,
            Guid.NewGuid()
        );

        TestValidationResult<CancelSubscriptionCommand> result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SubscriptionId)
            .WithErrorMessage("Subscription ID is required");
    }
}
