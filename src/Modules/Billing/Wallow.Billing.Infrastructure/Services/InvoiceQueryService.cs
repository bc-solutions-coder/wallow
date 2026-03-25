using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Services;

public sealed class InvoiceQueryService : IInvoiceQueryService
{
    private readonly IReadDbContext<BillingDbContext> _readDbContext;

    public InvoiceQueryService(IReadDbContext<BillingDbContext> readDbContext)
    {
        _readDbContext = readDbContext;
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        decimal result = await _readDbContext.Context.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= from && i.PaidAt < to)
            .SumAsync(i => i.TotalAmount.Amount, ct);

        return result;
    }

    public async Task<int> GetCountAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        int result = await _readDbContext.Context.Invoices
            .Where(i => i.CreatedAt >= from && i.CreatedAt < to)
            .CountAsync(ct);

        return result;
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        int result = await _readDbContext.Context.Invoices
            .Where(i => i.Status == InvoiceStatus.Issued)
            .CountAsync(ct);

        return result;
    }

    public async Task<decimal> GetOutstandingAmountAsync(CancellationToken ct = default)
    {
        decimal result = await _readDbContext.Context.Invoices
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Overdue)
            .SumAsync(i => i.TotalAmount.Amount, ct);

        return result;
    }
}
