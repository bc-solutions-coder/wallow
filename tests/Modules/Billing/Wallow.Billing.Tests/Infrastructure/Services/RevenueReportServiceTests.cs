using System.Reflection;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Services;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Persistence;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Billing.Tests.Infrastructure.Services;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class RevenueReportServiceTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    private TenantId _otherTenantId;

    protected override bool UseMigrateAsync => true;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _otherTenantId = TenantId.New();
    }

    [Fact]
    public async Task GetRevenueAsync_WithPaidInvoicesInRange_ReturnsCorrectGrossRevenue()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-10);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice paidInvoice1 = CreateAndIssueInvoiceAtTime("INV-RV-G1", 1000m, "USD", fromDate.AddDays(1));
        paidInvoice1.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Invoice paidInvoice2 = CreateAndIssueInvoiceAtTime("INV-RV-G2", 500m, "USD", fromDate.AddDays(3));
        paidInvoice2.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Payment payment1 = CreateAndCompletePayment(paidInvoice1, 1000m, PaymentMethod.CreditCard, fromDate.AddDays(2));
        Payment payment2 = CreateAndCompletePayment(paidInvoice2, 500m, PaymentMethod.BankTransfer, fromDate.AddDays(4));

        await DbContext.Invoices.AddRangeAsync(paidInvoice1, paidInvoice2);
        await DbContext.Payments.AddRangeAsync(payment1, payment2);
        await DbContext.SaveChangesAsync();

        RevenueReportService service = CreateRevenueReportService();

        IReadOnlyList<RevenueReportRow> results = await service.GetRevenueAsync(fromDate, toDate);

        results.Should().HaveCount(1);
        RevenueReportRow row = results[0];
        row.GrossRevenue.Should().Be(1500m);
        row.PaymentCount.Should().Be(2);
    }

    [Fact]
    public async Task GetRevenueAsync_WithNoInvoicesInRange_ReturnsZeroRow()
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
    public async Task GetRevenueAsync_TenantIsolation_DoesNotAggregateOtherTenantRevenue()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice paidInvoice = CreateAndIssueInvoiceAtTime("INV-RV-ISO1", 500m, "USD", fromDate.AddDays(1));
        paidInvoice.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);
        await DbContext.Invoices.AddAsync(paidInvoice);
        await DbContext.SaveChangesAsync();

        await using BillingDbContext otherDbContext = CreateDbContextForTenant(_otherTenantId);
        Invoice otherInvoice = CreateAndIssueInvoiceAtTime("INV-RV-ISO2", 1000m, "USD", fromDate.AddDays(2));
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

    #region Service Factory

    private RevenueReportService CreateRevenueReportService()
    {
        IReadDbContext<BillingDbContext> readDbContext = new ReadDbContext<BillingDbContext>(DbContext);
        return (RevenueReportService)Activator.CreateInstance(
            typeof(RevenueReportService),
            readDbContext)!;
    }

    #endregion

    #region Entity Helpers

    private Invoice CreateAndIssueInvoice(string invoiceNumber, decimal amount, string currency)
    {
        Invoice invoice = Invoice.Create(TestUserId, invoiceNumber, currency, TestUserId, TimeProvider.System);
        invoice.AddLineItem("Test Item", Money.Create(amount, currency), 1, TestUserId, TimeProvider.System);
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

    #endregion
}
