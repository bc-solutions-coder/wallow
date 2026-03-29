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
public class InvoiceReportServiceTests(PostgresContainerFixture fixture)
    : DbContextIntegrationTestBase<BillingDbContext>(fixture)
{
    protected override bool UseMigrateAsync => true;

    private IInvoiceReportService _sut = null!;

    private readonly DateTime _fromDate = DateTime.UtcNow.AddDays(-10);
    private readonly DateTime _toDate = DateTime.UtcNow.AddDays(1);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        IReadDbContext<BillingDbContext> readDbContext = new ReadDbContext<BillingDbContext>(DbContext);
        _sut = InvoiceReportServiceFactory.Create(readDbContext);

        await SeedInvoicesAsync();
    }

    [Fact]
    public async Task GetInvoicesAsync_ExcludesDraftInvoices()
    {
        IReadOnlyList<InvoiceReportRow> result = await _sut.GetInvoicesAsync(_fromDate, _toDate);

        result.Should().NotContain(r => r.InvoiceNumber == "INV-DRAFT");
    }

    [Fact]
    public async Task GetInvoicesAsync_RespectsDateRange()
    {
        DateTime narrowFrom = _fromDate.AddDays(3);
        DateTime narrowTo = _fromDate.AddDays(6);

        IReadOnlyList<InvoiceReportRow> result = await _sut.GetInvoicesAsync(narrowFrom, narrowTo);

        // Only invoices created between day+3 and day+6 should be returned (and not Draft)
        result.Should().AllSatisfy(r => r.InvoiceNumber.Should().NotBe("INV-DRAFT"));
        result.Should().NotContain(r => r.InvoiceNumber == "INV-ISSUED");
    }

    [Fact]
    public async Task GetInvoicesAsync_ReturnsRowsOrderedByCreatedAtDescending()
    {
        IReadOnlyList<InvoiceReportRow> result = await _sut.GetInvoicesAsync(_fromDate, _toDate);

        result.Should().HaveCountGreaterThan(1);
        List<DateTime> issueDates = result.Select(r => r.IssueDate).ToList();
        issueDates.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task GetInvoicesAsync_ExcludesDraftAndRespectsDateRange_ReturnsOnlyNonDraftInRange()
    {
        IReadOnlyList<InvoiceReportRow> result = await _sut.GetInvoicesAsync(_fromDate, _toDate);

        // 3 non-draft invoices in range: issued, paid, overdue
        result.Should().HaveCount(3);
        result.Should().Contain(r => r.InvoiceNumber == "INV-ISSUED");
        result.Should().Contain(r => r.InvoiceNumber == "INV-PAID");
        result.Should().Contain(r => r.InvoiceNumber == "INV-OVERDUE");
    }

    private async Task SeedInvoicesAsync()
    {
        Invoice draftInvoice = CreateInvoiceAtTime("INV-DRAFT", 100m, "USD", _fromDate.AddDays(1));

        Invoice issuedInvoice = CreateInvoiceAtTime("INV-ISSUED", 200m, "USD", _fromDate.AddDays(2));
        issuedInvoice.Issue(TestUserId, TimeProvider.System);

        Invoice paidInvoice = CreateInvoiceAtTime("INV-PAID", 500m, "USD", _fromDate.AddDays(4));
        paidInvoice.Issue(TestUserId, TimeProvider.System);
        paidInvoice.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

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
}

/// <summary>
/// Factory that creates InvoiceReportService from IReadDbContext.
/// Will fail at runtime until InvoiceReportService is migrated to accept IReadDbContext.
/// </summary>
internal static class InvoiceReportServiceFactory
{
    internal static IInvoiceReportService Create(IReadDbContext<BillingDbContext> readDbContext)
    {
        // The service currently requires (BillingDbContext, ITenantContext).
        // This factory attempts construction with IReadDbContext, which will throw
        // until the service is migrated to accept IReadDbContext<BillingDbContext>.
        return (IInvoiceReportService)Activator.CreateInstance(
            typeof(InvoiceReportService),
            readDbContext)!;
    }
}
