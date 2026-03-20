using Wallow.Billing.Api.Contracts.Invoices;
using Wallow.Billing.Api.Contracts.Payments;
using Wallow.Billing.Api.Contracts.Subscriptions;
using Wallow.Billing.Api.Controllers;
using Wallow.Billing.Domain.Metering.Enums;

namespace Wallow.Billing.Tests.Api.Contracts;

public class RequestContractTests
{
    #region CreateInvoiceRequest

    [Fact]
    public void CreateInvoiceRequest_WithAllFields_CreatesInstance()
    {
        DateTime dueDate = DateTime.UtcNow.AddDays(30);
        CreateInvoiceRequest request = new("INV-001", "USD", dueDate);

        request.InvoiceNumber.Should().Be("INV-001");
        request.Currency.Should().Be("USD");
        request.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void CreateInvoiceRequest_WithNullDueDate_CreatesInstance()
    {
        CreateInvoiceRequest request = new("INV-001", "EUR", null);

        request.InvoiceNumber.Should().Be("INV-001");
        request.Currency.Should().Be("EUR");
        request.DueDate.Should().BeNull();
    }

    #endregion

    #region AddLineItemRequest

    [Fact]
    public void AddLineItemRequest_WithAllFields_CreatesInstance()
    {
        AddLineItemRequest request = new("Consulting Services", 150.00m, 3);

        request.Description.Should().Be("Consulting Services");
        request.UnitPrice.Should().Be(150.00m);
        request.Quantity.Should().Be(3);
    }

    #endregion

    #region ProcessPaymentRequest

    [Fact]
    public void ProcessPaymentRequest_WithAllFields_CreatesInstance()
    {
        ProcessPaymentRequest request = new(250.00m, "USD", "CreditCard");

        request.Amount.Should().Be(250.00m);
        request.Currency.Should().Be("USD");
        request.PaymentMethod.Should().Be("CreditCard");
    }

    #endregion

    #region CreateSubscriptionRequest

    [Fact]
    public void CreateSubscriptionRequest_WithAllFields_CreatesInstance()
    {
        DateTime startDate = DateTime.UtcNow;
        DateTime periodEnd = DateTime.UtcNow.AddMonths(1);
        CreateSubscriptionRequest request = new("Enterprise", 199.99m, "EUR", startDate, periodEnd);

        request.PlanName.Should().Be("Enterprise");
        request.Price.Should().Be(199.99m);
        request.Currency.Should().Be("EUR");
        request.StartDate.Should().Be(startDate);
        request.PeriodEnd.Should().Be(periodEnd);
    }

    #endregion

    #region SetQuotaOverrideRequest

    [Fact]
    public void SetQuotaOverrideRequest_WithAllFields_CreatesInstance()
    {
        SetQuotaOverrideRequest request = new("api-calls", 5000, QuotaPeriod.Monthly, QuotaAction.Block);

        request.MeterCode.Should().Be("api-calls");
        request.Limit.Should().Be(5000);
        request.Period.Should().Be(QuotaPeriod.Monthly);
        request.OnExceeded.Should().Be(QuotaAction.Block);
    }

    [Fact]
    public void SetQuotaOverrideRequest_WithDailyPeriodAndWarnAction_CreatesInstance()
    {
        SetQuotaOverrideRequest request = new("storage", 10000, QuotaPeriod.Daily, QuotaAction.Warn);

        request.MeterCode.Should().Be("storage");
        request.Limit.Should().Be(10000);
        request.Period.Should().Be(QuotaPeriod.Daily);
        request.OnExceeded.Should().Be(QuotaAction.Warn);
    }

    #endregion
}
