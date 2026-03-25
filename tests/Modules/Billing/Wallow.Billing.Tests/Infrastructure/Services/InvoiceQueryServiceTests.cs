using System.Reflection;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Services;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Persistence;
using Wallow.Tests.Common.Bases;
using Wallow.Tests.Common.Fixtures;

namespace Wallow.Billing.Tests.Infrastructure.Services;

[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class InvoiceQueryServiceTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private IInvoiceQueryService _sut = null!;

    private readonly DateTime _fromDate = DateTime.UtcNow.AddDays(-10);
    private readonly DateTime _toDate = DateTime.UtcNow.AddDays(1);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        IReadDbContext<BillingDbContext> readDbContext = new ReadDbContext<BillingDbContext>(DbContext);
        _sut = InvoiceQueryServiceFactory.Create(readDbContext);

        await SeedInvoicesAsync();
    }

    [Fact]
    public async Task GetTotalRevenueAsync_WithPaidAndDraftInvoices_ReturnsOnlyPaidAmount()
    {
        decimal revenue = await _sut.GetTotalRevenueAsync(_fromDate, _toDate);

        revenue.Should().Be(500m);
    }

    [Fact]
    public async Task GetCountAsync_WithDateRange_CountsByCreatedAt()
    {
        int count = await _sut.GetCountAsync(_fromDate, _toDate);

        // All 4 invoices (draft, issued, paid, overdue) are within the date range
        count.Should().Be(4);
    }

    [Fact]
    public async Task GetPendingCountAsync_ReturnsCountOfIssuedInvoices()
    {
        int pendingCount = await _sut.GetPendingCountAsync();

        // Only the Issued invoice (status 1) counts as pending
        pendingCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOutstandingAmountAsync_SumsIssuedAndOverdueAmounts()
    {
        decimal outstanding = await _sut.GetOutstandingAmountAsync();

        // Issued (200m) + Overdue (150m)
        outstanding.Should().Be(350m);
    }

    private async Task SeedInvoicesAsync()
    {
        Invoice draftInvoice = CreateInvoiceAtTime("INV-DRAFT", 100m, "USD", _fromDate.AddDays(1));

        Invoice issuedInvoice = CreateInvoiceAtTime("INV-ISSUED", 200m, "USD", _fromDate.AddDays(2));
        issuedInvoice.Issue(TestUserId, TimeProvider.System);

        Invoice paidInvoice = CreateInvoiceAtTime("INV-PAID", 500m, "USD", _fromDate.AddDays(3));
        paidInvoice.Issue(TestUserId, TimeProvider.System);
        paidInvoice.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);
        SetPaidAt(paidInvoice, _fromDate.AddDays(4));

        Invoice overdueInvoice = CreateInvoiceAtTime("INV-OVERDUE", 150m, "USD", _fromDate.AddDays(5));
        overdueInvoice.Issue(TestUserId, TimeProvider.System);
        overdueInvoice.MarkAsOverdue(TestUserId, TimeProvider.System);

        await DbContext.Invoices.AddRangeAsync(draftInvoice, issuedInvoice, paidInvoice, overdueInvoice);
        await DbContext.SaveChangesAsync();
    }

    private Invoice CreateInvoiceAtTime(string invoiceNumber, decimal amount, string currency, DateTime createdAt)
    {
        Invoice invoice = Invoice.Create(TestUserId, invoiceNumber, currency, TestUserId, TimeProvider.System);
        invoice.AddLineItem("Test Item", Money.Create(amount, currency), 1, TestUserId, TimeProvider.System);

        PropertyInfo? createdAtProperty = typeof(Invoice).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(invoice, createdAt);

        return invoice;
    }

    private static void SetPaidAt(Invoice invoice, DateTime paidAt)
    {
        PropertyInfo? paidAtProperty = typeof(Invoice).GetProperty("PaidAt");
        paidAtProperty?.SetValue(invoice, paidAt);
    }
}

/// <summary>
/// Factory that creates InvoiceQueryService from IReadDbContext.
/// Will fail at runtime until InvoiceQueryService is migrated to accept IReadDbContext.
/// </summary>
internal static class InvoiceQueryServiceFactory
{
    internal static IInvoiceQueryService Create(IReadDbContext<BillingDbContext> readDbContext)
    {
        // The service currently requires (BillingDbContext, ITenantContext).
        // This factory attempts construction with IReadDbContext, which will throw
        // until the service is migrated to accept IReadDbContext<BillingDbContext>.
        return (IInvoiceQueryService)Activator.CreateInstance(
            typeof(InvoiceQueryService),
            readDbContext)!;
    }
}
