using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Services;

public sealed class RevenueReportService : IRevenueReportService
{
    private readonly IReadDbContext<BillingDbContext> _readDbContext;

    public RevenueReportService(IReadDbContext<BillingDbContext> readDbContext)
    {
        _readDbContext = readDbContext;
    }

    public async Task<IReadOnlyList<RevenueReportRow>> GetRevenueAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        string period = $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";

        BillingDbContext context = _readDbContext.Context;

        IQueryable<Invoice> invoicesInRange = context.Invoices
            .Where(i => i.CreatedAt >= from && i.CreatedAt < to);

        decimal grossRevenue = await invoicesInRange
            .Where(i => i.Status == InvoiceStatus.Paid)
            .SumAsync(i => i.TotalAmount.Amount, ct);

        int invoiceCount = await invoicesInRange
            .Where(i => i.Status == InvoiceStatus.Paid)
            .CountAsync(ct);

        string currency = await invoicesInRange
            .Select(i => i.TotalAmount.Currency)
            .FirstOrDefaultAsync(ct) ?? "USD";

        List<InvoiceId> invoiceIds = await invoicesInRange
            .Select(i => i.Id)
            .ToListAsync(ct);

        int paymentCount = await context.Payments
            .Where(p => invoiceIds.Contains(p.InvoiceId))
            .CountAsync(ct);

        decimal refunds = await context.Payments
            .Where(p => invoiceIds.Contains(p.InvoiceId))
            .Where(p => p.Status == PaymentStatus.Refunded)
            .SumAsync(p => p.Amount.Amount, ct);

        decimal netRevenue = grossRevenue - refunds;

        RevenueReportRow row = new(period, grossRevenue, netRevenue, refunds, currency, invoiceCount, paymentCount);

        return new List<RevenueReportRow> { row };
    }
}
