using System.Data.Common;
using System.Reflection;
using Dapper;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Billing.Infrastructure.Persistence;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Kernel.Identity;
using Foundry.Tests.Common.Bases;
using Foundry.Tests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Tests;

[CollectionDefinition("PostgresDatabase")]
public class PostgresDatabaseCollection : ICollectionFixture<PostgresContainerFixture>;

/// <summary>
/// Tests Dapper-based reporting queries against a real database.
/// Verifies complex SQL correctness, multi-tenancy isolation, and aggregation logic.
/// NOTE: These tests use corrected SQL (lowercase table names) as the production services
/// currently have incorrect SQL (Pascal case with quotes).
/// </summary>
[Collection("PostgresDatabase")]
[Trait("Category", "Integration")]
public class DapperQueryTests : DbContextIntegrationTestBase<BillingDbContext>
{
    private TenantId _otherTenantId;

    public DapperQueryTests(PostgresContainerFixture fixture) : base(fixture) { }

    protected override bool UseMigrateAsync => true;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _otherTenantId = TenantId.New();
    }

    [Fact]
    public async Task PaymentReport_ReturnsPaymentsInDateRange()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-10);
        DateTime toDate = DateTime.UtcNow;

        Invoice invoice1 = CreateAndIssueInvoice("INV-001", 100m, "USD");
        Invoice invoice2 = CreateAndIssueInvoice("INV-002", 200m, "USD");
        Invoice invoice3 = CreateAndIssueInvoice("INV-003", 300m, "USD");

        Payment payment1 = CreateAndCompletePayment(invoice1, 100m, PaymentMethod.CreditCard, fromDate.AddDays(1));
        Payment payment2 = CreateAndCompletePayment(invoice2, 200m, PaymentMethod.BankTransfer, fromDate.AddDays(5));
        Payment payment3 = CreateAndCompletePayment(invoice3, 300m, PaymentMethod.CreditCard, fromDate.AddDays(-15));

        await DbContext.Invoices.AddRangeAsync(invoice1, invoice2, invoice3);
        await DbContext.Payments.AddRangeAsync(payment1, payment2, payment3);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                p.id as PaymentId,
                i.invoice_number as InvoiceNumber,
                p.amount as Amount,
                p.currency as Currency,
                CASE p.method WHEN 0 THEN 'CreditCard' WHEN 1 THEN 'BankTransfer' WHEN 2 THEN 'PayPal' END as Method,
                CASE p.status WHEN 0 THEN 'Pending' WHEN 1 THEN 'Completed' WHEN 2 THEN 'Failed' WHEN 3 THEN 'Refunded' END as Status,
                p.completed_at as PaymentDate
            FROM billing.payments p
            JOIN billing.invoices i ON i.id = p.invoice_id
            WHERE p.tenant_id = @TenantId
              AND p.created_at >= @From
              AND p.created_at < @To
            ORDER BY p.created_at DESC
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<PaymentReportRow> results = await connection.QueryAsync<PaymentReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<PaymentReportRow> list = results.ToList();
        list.Should().HaveCount(2);
        list.Should().Contain(p => p.InvoiceNumber == "INV-001" && p.Amount == 100m);
        list.Should().Contain(p => p.InvoiceNumber == "INV-002" && p.Amount == 200m);
        list.Should().NotContain(p => p.InvoiceNumber == "INV-003");
    }

    [Fact]
    public async Task PaymentReport_ReturnsCorrectPaymentDetails()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);
        DateTime completedDate = DateTime.UtcNow.AddDays(-2);

        Invoice invoice = CreateAndIssueInvoice("INV-DETAILS", 150m, "EUR");
        Payment payment = CreateAndCompletePayment(invoice, 150m, PaymentMethod.PayPal, completedDate);

        await DbContext.Invoices.AddAsync(invoice);
        await DbContext.Payments.AddAsync(payment);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                p.id as PaymentId,
                i.invoice_number as InvoiceNumber,
                p.amount as Amount,
                p.currency as Currency,
                CASE p.method WHEN 0 THEN 'CreditCard' WHEN 1 THEN 'BankTransfer' WHEN 2 THEN 'PayPal' END as Method,
                CASE p.status WHEN 0 THEN 'Pending' WHEN 1 THEN 'Completed' WHEN 2 THEN 'Failed' WHEN 3 THEN 'Refunded' END as Status,
                p.completed_at as PaymentDate
            FROM billing.payments p
            JOIN billing.invoices i ON i.id = p.invoice_id
            WHERE p.tenant_id = @TenantId
              AND p.created_at >= @From
              AND p.created_at < @To
            ORDER BY p.created_at DESC
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<PaymentReportRow> results = await connection.QueryAsync<PaymentReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<PaymentReportRow> list = results.ToList();
        list.Should().HaveCount(1);
        PaymentReportRow result = list.First();
        result.PaymentId.Should().Be(payment.Id.Value);
        result.InvoiceNumber.Should().Be("INV-DETAILS");
        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("EUR");
        result.Method.Should().Be(PaymentMethod.PayPal.ToString());
        result.Status.Should().Be(PaymentStatus.Completed.ToString());
        result.PaymentDate.Should().BeCloseTo(completedDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task PaymentReport_RespectsTenantIsolation()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice invoice1 = CreateAndIssueInvoice("INV-T1", 100m, "USD");
        Payment payment1 = CreateAndCompletePayment(invoice1, 100m, PaymentMethod.CreditCard, DateTime.UtcNow.AddDays(-2));
        await DbContext.Invoices.AddAsync(invoice1);
        await DbContext.Payments.AddAsync(payment1);
        await DbContext.SaveChangesAsync();

        await using BillingDbContext otherDbContext = CreateDbContextForTenant(_otherTenantId);

        Invoice invoice2 = CreateAndIssueInvoice("INV-T2", 200m, "USD");
        Payment payment2 = CreateAndCompletePayment(invoice2, 200m, PaymentMethod.BankTransfer, DateTime.UtcNow.AddDays(-2));
        await otherDbContext.Invoices.AddAsync(invoice2);
        await otherDbContext.Payments.AddAsync(payment2);
        await otherDbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                p.id as PaymentId,
                i.invoice_number as InvoiceNumber,
                p.amount as Amount,
                p.currency as Currency,
                CASE p.method WHEN 0 THEN 'CreditCard' WHEN 1 THEN 'BankTransfer' WHEN 2 THEN 'PayPal' END as Method,
                CASE p.status WHEN 0 THEN 'Pending' WHEN 1 THEN 'Completed' WHEN 2 THEN 'Failed' WHEN 3 THEN 'Refunded' END as Status,
                p.completed_at as PaymentDate
            FROM billing.payments p
            JOIN billing.invoices i ON i.id = p.invoice_id
            WHERE p.tenant_id = @TenantId
              AND p.created_at >= @From
              AND p.created_at < @To
            ORDER BY p.created_at DESC
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        List<PaymentReportRow> tenant1Results = (await connection.QueryAsync<PaymentReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate })).ToList();

        List<PaymentReportRow> tenant2Results = (await connection.QueryAsync<PaymentReportRow>(
            sql,
            new { TenantId = _otherTenantId.Value, From = fromDate, To = toDate })).ToList();

        tenant1Results.Should().HaveCount(1);
        tenant1Results.First().InvoiceNumber.Should().Be("INV-T1");

        tenant2Results.Should().HaveCount(1);
        tenant2Results.First().InvoiceNumber.Should().Be("INV-T2");
    }

    [Fact]
    public async Task InvoiceReport_ReturnsInvoicesInDateRange()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-10);
        DateTime toDate = DateTime.UtcNow;

        Invoice invoice1 = CreateAndIssueInvoiceAtTime("INV-R1", 100m, "USD", fromDate.AddDays(1));
        Invoice invoice2 = CreateAndIssueInvoiceAtTime("INV-R2", 200m, "USD", fromDate.AddDays(5));
        Invoice invoice3 = CreateAndIssueInvoiceAtTime("INV-R3", 300m, "USD", fromDate.AddDays(-15));

        await DbContext.Invoices.AddRangeAsync(invoice1, invoice2, invoice3);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                i.invoice_number as InvoiceNumber,
                'User_' || i.user_id as CustomerName,
                i.total_amount as Amount,
                i.currency as Currency,
                CASE i.status WHEN 0 THEN 'Draft' WHEN 1 THEN 'Issued' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Overdue' WHEN 4 THEN 'Cancelled' END as Status,
                i.created_at as IssueDate,
                i.due_date as DueDate
            FROM billing.invoices i
            WHERE i.tenant_id = @TenantId
              AND i.status != 0
              AND i.created_at >= @From
              AND i.created_at < @To
            ORDER BY i.created_at DESC
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<InvoiceReportRow> results = await connection.QueryAsync<InvoiceReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<InvoiceReportRow> list = results.ToList();
        list.Should().HaveCount(2);
        list.Should().Contain(i => i.InvoiceNumber == "INV-R1");
        list.Should().Contain(i => i.InvoiceNumber == "INV-R2");
        list.Should().NotContain(i => i.InvoiceNumber == "INV-R3");
    }

    [Fact]
    public async Task InvoiceReport_ExcludesDraftInvoices()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice draftInvoice = CreateInvoice("INV-DRAFT", 100m, "USD");
        Invoice issuedInvoice = CreateAndIssueInvoice("INV-ISSUED", 200m, "USD");

        await DbContext.Invoices.AddRangeAsync(draftInvoice, issuedInvoice);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                i.invoice_number as InvoiceNumber,
                'User_' || i.user_id as CustomerName,
                i.total_amount as Amount,
                i.currency as Currency,
                CASE i.status WHEN 0 THEN 'Draft' WHEN 1 THEN 'Issued' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Overdue' WHEN 4 THEN 'Cancelled' END as Status,
                i.created_at as IssueDate,
                i.due_date as DueDate
            FROM billing.invoices i
            WHERE i.tenant_id = @TenantId
              AND i.status != 0
              AND i.created_at >= @From
              AND i.created_at < @To
            ORDER BY i.created_at DESC
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<InvoiceReportRow> results = await connection.QueryAsync<InvoiceReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<InvoiceReportRow> list = results.ToList();
        list.Should().HaveCount(1);
        list.First().InvoiceNumber.Should().Be("INV-ISSUED");
    }

    [Fact]
    public async Task InvoiceReport_ReturnsCorrectInvoiceDetails()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);
        DateTime dueDate = DateTime.UtcNow.AddDays(30);

        Invoice invoice = Invoice.Create(TestUserId, "INV-DETAILS-2", "GBP", TestUserId, TimeProvider.System, dueDate);
        invoice.AddLineItem("Service", Money.Create(75m, "GBP"), 2, TestUserId, TimeProvider.System);
        invoice.Issue(TestUserId, TimeProvider.System);

        await DbContext.Invoices.AddAsync(invoice);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                i.invoice_number as InvoiceNumber,
                'User_' || i.user_id as CustomerName,
                i.total_amount as Amount,
                i.currency as Currency,
                CASE i.status WHEN 0 THEN 'Draft' WHEN 1 THEN 'Issued' WHEN 2 THEN 'Paid' WHEN 3 THEN 'Overdue' WHEN 4 THEN 'Cancelled' END as Status,
                i.created_at as IssueDate,
                i.due_date as DueDate
            FROM billing.invoices i
            WHERE i.tenant_id = @TenantId
              AND i.status != 0
              AND i.created_at >= @From
              AND i.created_at < @To
            ORDER BY i.created_at DESC
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<InvoiceReportRow> results = await connection.QueryAsync<InvoiceReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<InvoiceReportRow> list = results.ToList();
        list.Should().HaveCount(1);
        InvoiceReportRow result = list.First();
        result.InvoiceNumber.Should().Be("INV-DETAILS-2");
        result.CustomerName.Should().Be($"User_{TestUserId}");
        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("GBP");
        result.Status.Should().Be(InvoiceStatus.Issued.ToString());
        result.DueDate.Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RevenueReport_CalculatesRevenueCorrectly()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-10);
        DateTime toDate = DateTime.UtcNow;

        Invoice invoice1 = CreateAndIssueInvoiceAtTime("INV-REV1", 1000m, "USD", fromDate.AddDays(1));
        invoice1.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Invoice invoice2 = CreateAndIssueInvoiceAtTime("INV-REV2", 500m, "USD", fromDate.AddDays(3));
        invoice2.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Invoice invoice3 = CreateAndIssueInvoiceAtTime("INV-REV3", 200m, "USD", fromDate.AddDays(5));

        await DbContext.Invoices.AddRangeAsync(invoice1, invoice2, invoice3);

        Payment payment1 = CreateAndCompletePayment(invoice1, 1000m, PaymentMethod.CreditCard, fromDate.AddDays(2));
        Payment payment2 = CreateAndCompletePayment(invoice2, 500m, PaymentMethod.BankTransfer, fromDate.AddDays(4));
        Payment refundPayment = CreateAndRefundPayment(invoice1, 100m, PaymentMethod.CreditCard, fromDate.AddDays(6));

        await DbContext.Payments.AddRangeAsync(payment1, payment2, refundPayment);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                TO_CHAR(@From, 'YYYY-MM-DD') || ' to ' || TO_CHAR(@To, 'YYYY-MM-DD') AS Period,
                COALESCE((SELECT SUM(i2.total_amount) FROM billing.invoices i2 WHERE i2.tenant_id = @TenantId AND i2.status = 2 AND i2.created_at >= @From AND i2.created_at < @To), 0) AS GrossRevenue,
                COALESCE((SELECT SUM(i2.total_amount) FROM billing.invoices i2 WHERE i2.tenant_id = @TenantId AND i2.status = 2 AND i2.created_at >= @From AND i2.created_at < @To), 0) AS NetRevenue,
                COALESCE(SUM(CASE WHEN p.status = 3 THEN p.amount ELSE 0 END), 0) AS Refunds,
                COALESCE(MAX(i.currency), 'USD') AS Currency,
                COUNT(DISTINCT i.id)::int AS InvoiceCount,
                COUNT(DISTINCT p.id)::int AS PaymentCount
            FROM billing.invoices i
            LEFT JOIN billing.payments p ON p.invoice_id = i.id AND p.tenant_id = @TenantId
            WHERE i.tenant_id = @TenantId
              AND i.created_at >= @From
              AND i.created_at < @To
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<RevenueReportRow> results = await connection.QueryAsync<RevenueReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<RevenueReportRow> list = results.ToList();
        list.Should().HaveCount(1);
        RevenueReportRow result = list.First();
        result.GrossRevenue.Should().Be(1500m);
        result.NetRevenue.Should().Be(1500m);
        result.Refunds.Should().Be(100m);
        result.Currency.Should().Be("USD");
        result.InvoiceCount.Should().Be(3);
        result.PaymentCount.Should().Be(3);
    }

    [Fact]
    public async Task RevenueReport_OnlyCountsPaidInvoices()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        Invoice paidInvoice = CreateAndIssueInvoiceAtTime("INV-PAID", 500m, "USD", fromDate.AddDays(1));
        paidInvoice.MarkAsPaid(Guid.NewGuid(), TestUserId, TimeProvider.System);

        Invoice issuedInvoice = CreateAndIssueInvoiceAtTime("INV-ISSUED-2", 300m, "USD", fromDate.AddDays(2));

        Invoice overdueInvoice = CreateAndIssueInvoiceAtTime("INV-OVERDUE", 200m, "USD", fromDate.AddDays(3));
        overdueInvoice.MarkAsOverdue(TestUserId, TimeProvider.System);

        await DbContext.Invoices.AddRangeAsync(paidInvoice, issuedInvoice, overdueInvoice);
        await DbContext.SaveChangesAsync();

        const string sql = """
            SELECT
                TO_CHAR(@From, 'YYYY-MM-DD') || ' to ' || TO_CHAR(@To, 'YYYY-MM-DD') AS Period,
                COALESCE((SELECT SUM(i2.total_amount) FROM billing.invoices i2 WHERE i2.tenant_id = @TenantId AND i2.status = 2 AND i2.created_at >= @From AND i2.created_at < @To), 0) AS GrossRevenue,
                COALESCE((SELECT SUM(i2.total_amount) FROM billing.invoices i2 WHERE i2.tenant_id = @TenantId AND i2.status = 2 AND i2.created_at >= @From AND i2.created_at < @To), 0) AS NetRevenue,
                COALESCE(SUM(CASE WHEN p.status = 3 THEN p.amount ELSE 0 END), 0) AS Refunds,
                COALESCE(MAX(i.currency), 'USD') AS Currency,
                COUNT(DISTINCT i.id)::int AS InvoiceCount,
                COUNT(DISTINCT p.id)::int AS PaymentCount
            FROM billing.invoices i
            LEFT JOIN billing.payments p ON p.invoice_id = i.id AND p.tenant_id = @TenantId
            WHERE i.tenant_id = @TenantId
              AND i.created_at >= @From
              AND i.created_at < @To
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<RevenueReportRow> results = await connection.QueryAsync<RevenueReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<RevenueReportRow> list = results.ToList();
        list.Should().HaveCount(1);
        RevenueReportRow result = list.First();
        result.GrossRevenue.Should().Be(500m);
        result.NetRevenue.Should().Be(500m);
    }

    [Fact]
    public async Task RevenueReport_HandlesEmptyDataSet()
    {
        DateTime fromDate = DateTime.UtcNow.AddDays(-5);
        DateTime toDate = DateTime.UtcNow.AddDays(1);

        const string sql = """
            SELECT
                TO_CHAR(@From, 'YYYY-MM-DD') || ' to ' || TO_CHAR(@To, 'YYYY-MM-DD') AS Period,
                COALESCE((SELECT SUM(i2.total_amount) FROM billing.invoices i2 WHERE i2.tenant_id = @TenantId AND i2.status = 2 AND i2.created_at >= @From AND i2.created_at < @To), 0) AS GrossRevenue,
                COALESCE((SELECT SUM(i2.total_amount) FROM billing.invoices i2 WHERE i2.tenant_id = @TenantId AND i2.status = 2 AND i2.created_at >= @From AND i2.created_at < @To), 0) AS NetRevenue,
                COALESCE(SUM(CASE WHEN p.status = 3 THEN p.amount ELSE 0 END), 0) AS Refunds,
                COALESCE(MAX(i.currency), 'USD') AS Currency,
                COUNT(DISTINCT i.id)::int AS InvoiceCount,
                COUNT(DISTINCT p.id)::int AS PaymentCount
            FROM billing.invoices i
            LEFT JOIN billing.payments p ON p.invoice_id = i.id AND p.tenant_id = @TenantId
            WHERE i.tenant_id = @TenantId
              AND i.created_at >= @From
              AND i.created_at < @To
            """;

        await using DbConnection connection = DbContext.Database.GetDbConnection();
        IEnumerable<RevenueReportRow> results = await connection.QueryAsync<RevenueReportRow>(
            sql,
            new { TenantId = TestTenantId.Value, From = fromDate, To = toDate });

        List<RevenueReportRow> list = results.ToList();
        list.Should().HaveCount(1);
        RevenueReportRow result = list.First();
        result.GrossRevenue.Should().Be(0m);
        result.NetRevenue.Should().Be(0m);
        result.Refunds.Should().Be(0m);
        result.Currency.Should().Be("USD");
        result.InvoiceCount.Should().Be(0);
        result.PaymentCount.Should().Be(0);
    }

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
        Invoice invoice = CreateInvoice(invoiceNumber, amount, currency);
        invoice.Issue(TestUserId, TimeProvider.System);

        PropertyInfo? createdAtProperty = typeof(Invoice).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(invoice, createdAt);

        return invoice;
    }

    private Payment CreateAndCompletePayment(Invoice invoice, decimal amount, PaymentMethod method, DateTime completedAt)
    {
        Payment payment = Payment.Create(invoice.Id, invoice.UserId, Money.Create(amount, invoice.TotalAmount.Currency), method, TestUserId, TimeProvider.System);

        payment.Complete("txn-" + Guid.NewGuid().ToString(), TestUserId, TimeProvider.System);

        PropertyInfo? completedAtProperty = typeof(Payment).GetProperty("CompletedAt");
        completedAtProperty?.SetValue(payment, completedAt);

        PropertyInfo? createdAtProperty = typeof(Payment).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(payment, completedAt);

        return payment;
    }

    private Payment CreateAndRefundPayment(Invoice invoice, decimal amount, PaymentMethod method, DateTime refundedAt)
    {
        Payment payment = Payment.Create(invoice.Id, invoice.UserId, Money.Create(amount, invoice.TotalAmount.Currency), method, TestUserId, TimeProvider.System);

        payment.Complete("txn-" + Guid.NewGuid().ToString(), TestUserId, TimeProvider.System);
        payment.Refund(TestUserId, TimeProvider.System);

        PropertyInfo? createdAtProperty = typeof(Payment).GetProperty("CreatedAt");
        createdAtProperty?.SetValue(payment, refundedAt);

        return payment;
    }
}
