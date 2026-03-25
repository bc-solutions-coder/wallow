using System.Reflection;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Services;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Billing.Tests.Infrastructure.Services;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class BillingReportServiceTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    private TenantId _otherTenantId;

    protected override bool UseMigrateAsync => true;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _otherTenantId = TenantId.New();
    }

    #region PaymentReportService Tests

    [Fact]
    public async Task GetPaymentsAsync_WithPaymentsInRange_ReturnsOnlyInRangePayments()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-10);
        DateTime toDate = DateTime.UtcNow;

        Invoice invoice1 = CreateAndIssueInvoice("INV-PR-001", 100m, "USD");
        Invoice invoice2 = CreateAndIssueInvoice("INV-PR-002", 200m, "USD");
        Invoice invoice3 = CreateAndIssueInvoice("INV-PR-003", 300m, "USD");

        Payment payment1 = CreateAndCompletePayment(invoice1, 100m, PaymentMethod.CreditCard, fromDate.AddDays(1));
        Payment payment2 = CreateAndCompletePayment(invoice2, 200m, PaymentMethod.BankTransfer, fromDate.AddDays(5));
        Payment paymentOutOfRange = CreateAndCompletePayment(invoice3, 300m, PaymentMethod.CreditCard, fromDate.AddDays(-15));

        await DbContext.Invoices.AddRangeAsync(invoice1, invoice2, invoice3);
        await DbContext.Payments.AddRangeAsync(payment1, payment2, paymentOutOfRange);
        await DbContext.SaveChangesAsync();

        PaymentReportService service = CreatePaymentReportService();

        IReadOnlyList<PaymentReportRow> results = await service.GetPaymentsAsync(fromDate, toDate);

        results.Should().HaveCount(2);
        results.Should().Contain(p => p.InvoiceNumber == "INV-PR-001" && p.Amount == 100m);
        results.Should().Contain(p => p.InvoiceNumber == "INV-PR-002" && p.Amount == 200m);
        results.Should().NotContain(p => p.InvoiceNumber == "INV-PR-003");
    }

    [Fact]
    public async Task GetPaymentsAsync_WithPaymentsInRange_ReturnsCorrectFields()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);
        DateTime completedDate = DateTime.UtcNow.AddDays(-2);

        Invoice invoice = CreateAndIssueInvoice("INV-PR-FIELDS", 150m, "EUR");
        Payment payment = CreateAndCompletePayment(invoice, 150m, PaymentMethod.BankTransfer, completedDate);

        await DbContext.Invoices.AddAsync(invoice);
        await DbContext.Payments.AddAsync(payment);
        await DbContext.SaveChangesAsync();

        PaymentReportService service = CreatePaymentReportService();

        IReadOnlyList<PaymentReportRow> results = await service.GetPaymentsAsync(fromDate, toDate);

        results.Should().HaveCount(1);
        PaymentReportRow row = results[0];
        row.InvoiceNumber.Should().Be("INV-PR-FIELDS");
        row.Amount.Should().Be(150m);
        row.Method.Should().Be(nameof(PaymentMethod.BankTransfer));
        row.Status.Should().Be(nameof(PaymentStatus.Completed));
    }

    [Fact]
    public async Task GetPaymentsAsync_WithOtherTenantPayments_ReturnOnlyCurrentTenantPayments()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice invoice1 = CreateAndIssueInvoice("INV-PR-T1", 100m, "USD");
        Payment payment1 = CreateAndCompletePayment(invoice1, 100m, PaymentMethod.CreditCard, DateTime.UtcNow.AddDays(-2));
        await DbContext.Invoices.AddAsync(invoice1);
        await DbContext.Payments.AddAsync(payment1);
        await DbContext.SaveChangesAsync();

        await using BillingDbContext otherDbContext = CreateDbContextForTenant(_otherTenantId);
        Invoice invoice2 = CreateAndIssueInvoice("INV-PR-T2", 200m, "USD");
        Payment payment2 = CreateAndCompletePayment(invoice2, 200m, PaymentMethod.BankTransfer, DateTime.UtcNow.AddDays(-2));
        await otherDbContext.Invoices.AddAsync(invoice2);
        await otherDbContext.Payments.AddAsync(payment2);
        await otherDbContext.SaveChangesAsync();

        PaymentReportService service = CreatePaymentReportService();

        IReadOnlyList<PaymentReportRow> results = await service.GetPaymentsAsync(fromDate, toDate);

        results.Should().HaveCount(1);
        results[0].InvoiceNumber.Should().Be("INV-PR-T1");
    }

    #endregion

    #region RevenueReportService Tests

    [Fact]
    public async Task GetRevenueAsync_WithMixedStatuses_ReportsCorrectAmounts()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-10);
        DateTime toDate = DateTime.UtcNow;

        Invoice paidInvoice1 = CreateAndIssueInvoiceAtTime("INV-RV-PAID1", 1000m, "USD", fromDate.AddDays(1));
        paidInvoice1.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Invoice paidInvoice2 = CreateAndIssueInvoiceAtTime("INV-RV-PAID2", 500m, "USD", fromDate.AddDays(3));
        paidInvoice2.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Invoice issuedInvoice = CreateAndIssueInvoiceAtTime("INV-RV-ISSUED", 200m, "USD", fromDate.AddDays(5));

        await DbContext.Invoices.AddRangeAsync(paidInvoice1, paidInvoice2, issuedInvoice);

        Payment payment1 = CreateAndCompletePayment(paidInvoice1, 1000m, PaymentMethod.CreditCard, fromDate.AddDays(2));
        Payment payment2 = CreateAndCompletePayment(paidInvoice2, 500m, PaymentMethod.BankTransfer, fromDate.AddDays(4));
        Payment refundPayment = CreateAndRefundPayment(paidInvoice1, 100m, PaymentMethod.CreditCard, fromDate.AddDays(6));

        await DbContext.Payments.AddRangeAsync(payment1, payment2, refundPayment);
        await DbContext.SaveChangesAsync();

        RevenueReportService service = CreateRevenueReportService();

        IReadOnlyList<RevenueReportRow> results = await service.GetRevenueAsync(fromDate, toDate);

        results.Should().HaveCount(1);
        RevenueReportRow row = results[0];
        row.GrossRevenue.Should().Be(1500m);
        row.Refunds.Should().Be(100m);
        row.InvoiceCount.Should().Be(2);
    }

    [Fact]
    public async Task GetRevenueAsync_WithEmptyDataset_ReturnsSingleRowWithZeros()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        RevenueReportService service = CreateRevenueReportService();

        IReadOnlyList<RevenueReportRow> results = await service.GetRevenueAsync(fromDate, toDate);

        results.Should().HaveCount(1);
        RevenueReportRow row = results[0];
        row.GrossRevenue.Should().Be(0m);
        row.NetRevenue.Should().Be(0m);
        row.Refunds.Should().Be(0m);
        row.InvoiceCount.Should().Be(0);
        row.PaymentCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRevenueAsync_WithOtherTenantData_ExcludesOtherTenantRevenue()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice paidInvoice = CreateAndIssueInvoiceAtTime("INV-RV-T1", 500m, "USD", fromDate.AddDays(1));
        paidInvoice.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);
        await DbContext.Invoices.AddAsync(paidInvoice);
        await DbContext.SaveChangesAsync();

        await using BillingDbContext otherDbContext = CreateDbContextForTenant(_otherTenantId);
        Invoice otherInvoice = CreateAndIssueInvoiceAtTime("INV-RV-T2", 1000m, "USD", fromDate.AddDays(2));
        otherInvoice.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);
        await otherDbContext.Invoices.AddAsync(otherInvoice);
        await otherDbContext.SaveChangesAsync();

        RevenueReportService service = CreateRevenueReportService();

        IReadOnlyList<RevenueReportRow> results = await service.GetRevenueAsync(fromDate, toDate);

        results.Should().HaveCount(1);
        RevenueReportRow row = results[0];
        row.GrossRevenue.Should().Be(500m);
        row.InvoiceCount.Should().Be(1);
    }

    #endregion

    #region Service Factories

    private PaymentReportService CreatePaymentReportService()
    {
        IReadDbContext<BillingDbContext> readDbContext = new TestReadDbContext<BillingDbContext>(DbContext);
        return new PaymentReportService(readDbContext);
    }

    private RevenueReportService CreateRevenueReportService()
    {
        IReadDbContext<BillingDbContext> readDbContext = new TestReadDbContext<BillingDbContext>(DbContext);
        return new RevenueReportService(readDbContext);
    }

    #endregion

    #region Entity Helpers

    private Invoice CreateInvoice(string invoiceNumber, decimal amount, string currency)
    {
        Invoice invoice = Invoice.Create(TestUserId, invoiceNumber, currency, TestUserId, TimeProvider.System);
        invoice.AddLineItem("Test Item", Money.Create(amount, currency), 1, TestUserId, TimeProvider.System);
        return invoice;
    }

    private Invoice CreateAndIssueInvoice(string invoiceNumber, decimal amount, string currency)
    {
        Invoice invoice = CreateInvoice(invoiceNumber, amount, currency);
        invoice.Issue(TestUserId, TimeProvider.System);
        return invoice;
    }

    private Invoice CreateAndIssueInvoiceAtTime(string invoiceNumber, decimal amount, string currency, DateTime createdAt)
    {
        Invoice invoice = CreateAndIssueInvoice(invoiceNumber, amount, currency);

        PropertyInfo? createdAtProperty = typeof(Invoice).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(invoice, createdAt);

        return invoice;
    }

    private Payment CreateAndCompletePayment(Invoice invoice, decimal amount, PaymentMethod method, DateTime completedAt)
    {
        Payment payment = Payment.Create(
            invoice.Id, invoice.UserId,
            Money.Create(amount, invoice.TotalAmount.Currency),
            method, TestUserId, TimeProvider.System);

        payment.Complete("txn-" + Guid.NewGuid().ToString(), TestUserId, TimeProvider.System);

        PropertyInfo? completedAtProperty = typeof(Payment).GetProperty("CompletedAt");
        completedAtProperty?.SetValue(payment, completedAt);

        PropertyInfo? createdAtProperty = typeof(Payment).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(payment, completedAt);

        return payment;
    }

    private Payment CreateAndRefundPayment(Invoice invoice, decimal amount, PaymentMethod method, DateTime refundedAt)
    {
        Payment payment = Payment.Create(
            invoice.Id, invoice.UserId,
            Money.Create(amount, invoice.TotalAmount.Currency),
            method, TestUserId, TimeProvider.System);

        payment.Complete("txn-" + Guid.NewGuid().ToString(), TestUserId, TimeProvider.System);
        payment.Refund(TestUserId, TimeProvider.System);

        PropertyInfo? createdAtProperty = typeof(Payment).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(payment, refundedAt);

        return payment;
    }

    #endregion
}

/// <summary>
/// Simple IReadDbContext wrapper for testing. Wraps an existing DbContext instance.
/// </summary>
internal sealed class TestReadDbContext<TContext>(TContext context) : IReadDbContext<TContext>
    where TContext : Microsoft.EntityFrameworkCore.DbContext
{
    public TContext Context { get; } = context;
}
