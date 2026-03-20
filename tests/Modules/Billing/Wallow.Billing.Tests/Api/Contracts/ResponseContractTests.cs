using Wallow.Billing.Api.Contracts.Invoices;
using Wallow.Billing.Api.Contracts.Payments;
using Wallow.Billing.Api.Contracts.Subscriptions;

namespace Wallow.Billing.Tests.Api.Contracts;

public class ResponseContractTests
{
    #region InvoiceResponse

    [Fact]
    public void InvoiceResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime dueDate = DateTime.UtcNow.AddDays(30);
        DateTime paidAt = DateTime.UtcNow;
        DateTime createdAt = DateTime.UtcNow.AddDays(-1);
        DateTime updatedAt = DateTime.UtcNow;
        List<InvoiceLineItemResponse> lineItems = new()
        {
            new InvoiceLineItemResponse(Guid.NewGuid(), "Service A", 100.00m, "USD", 2, 200.00m)
        };

        InvoiceResponse response = new(id, userId, "INV-001", "Issued", 200.00m, "USD",
            dueDate, paidAt, createdAt, updatedAt, lineItems);

        response.Id.Should().Be(id);
        response.UserId.Should().Be(userId);
        response.InvoiceNumber.Should().Be("INV-001");
        response.Status.Should().Be("Issued");
        response.TotalAmount.Should().Be(200.00m);
        response.Currency.Should().Be("USD");
        response.DueDate.Should().Be(dueDate);
        response.PaidAt.Should().Be(paidAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
        response.LineItems.Should().HaveCount(1);
    }

    [Fact]
    public void InvoiceResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        InvoiceResponse response = new(Guid.NewGuid(), Guid.NewGuid(), "INV-001", "Draft", 0m, "USD",
            null, null, DateTime.UtcNow, null, []);

        response.DueDate.Should().BeNull();
        response.PaidAt.Should().BeNull();
        response.UpdatedAt.Should().BeNull();
        response.LineItems.Should().BeEmpty();
    }

    #endregion

    #region InvoiceLineItemResponse

    [Fact]
    public void InvoiceLineItemResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        InvoiceLineItemResponse response = new(id, "Consulting", 150.00m, "EUR", 3, 450.00m);

        response.Id.Should().Be(id);
        response.Description.Should().Be("Consulting");
        response.UnitPrice.Should().Be(150.00m);
        response.Currency.Should().Be("EUR");
        response.Quantity.Should().Be(3);
        response.LineTotal.Should().Be(450.00m);
    }

    #endregion

    #region PaymentResponse

    [Fact]
    public void PaymentResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        Guid invoiceId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime completedAt = DateTime.UtcNow;
        DateTime createdAt = DateTime.UtcNow.AddHours(-1);
        DateTime updatedAt = DateTime.UtcNow;

        PaymentResponse response = new(id, invoiceId, userId, 250.00m, "USD", "CreditCard",
            "Completed", "TXN-123", null, completedAt, createdAt, updatedAt);

        response.Id.Should().Be(id);
        response.InvoiceId.Should().Be(invoiceId);
        response.UserId.Should().Be(userId);
        response.Amount.Should().Be(250.00m);
        response.Currency.Should().Be("USD");
        response.Method.Should().Be("CreditCard");
        response.Status.Should().Be("Completed");
        response.TransactionReference.Should().Be("TXN-123");
        response.FailureReason.Should().BeNull();
        response.CompletedAt.Should().Be(completedAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void PaymentResponse_WithFailureReason_CreatesCorrectly()
    {
        PaymentResponse response = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m, "USD",
            "CreditCard", "Failed", null, "Insufficient funds", null, DateTime.UtcNow, DateTime.UtcNow);

        response.TransactionReference.Should().BeNull();
        response.FailureReason.Should().Be("Insufficient funds");
        response.CompletedAt.Should().BeNull();
    }

    #endregion

    #region SubscriptionResponse

    [Fact]
    public void SubscriptionResponse_WithAllFields_CreatesInstance()
    {
        Guid id = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        DateTime startDate = DateTime.UtcNow.AddDays(-30);
        DateTime endDate = DateTime.UtcNow.AddDays(335);
        DateTime periodStart = DateTime.UtcNow.AddDays(-30);
        DateTime periodEnd = DateTime.UtcNow;
        DateTime cancelledAt = DateTime.UtcNow.AddDays(-1);
        DateTime createdAt = DateTime.UtcNow.AddDays(-31);
        DateTime updatedAt = DateTime.UtcNow;

        SubscriptionResponse response = new(id, userId, "Enterprise", 999.99m, "EUR",
            "Cancelled", startDate, endDate, periodStart, periodEnd,
            cancelledAt, createdAt, updatedAt);

        response.Id.Should().Be(id);
        response.UserId.Should().Be(userId);
        response.PlanName.Should().Be("Enterprise");
        response.Price.Should().Be(999.99m);
        response.Currency.Should().Be("EUR");
        response.Status.Should().Be("Cancelled");
        response.StartDate.Should().Be(startDate);
        response.EndDate.Should().Be(endDate);
        response.CurrentPeriodStart.Should().Be(periodStart);
        response.CurrentPeriodEnd.Should().Be(periodEnd);
        response.CancelledAt.Should().Be(cancelledAt);
        response.CreatedAt.Should().Be(createdAt);
        response.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void SubscriptionResponse_WithNullOptionalFields_CreatesCorrectly()
    {
        DateTime now = DateTime.UtcNow;
        SubscriptionResponse response = new(Guid.NewGuid(), Guid.NewGuid(), "Pro", 29.99m, "USD",
            "Active", now, null, now, now.AddMonths(1), null, now, null);

        response.EndDate.Should().BeNull();
        response.CancelledAt.Should().BeNull();
        response.UpdatedAt.Should().BeNull();
    }

    #endregion
}
