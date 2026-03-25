using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.Persistence;

namespace Wallow.Billing.Infrastructure.Services;

public sealed class PaymentReportService : IPaymentReportService
{
    private readonly IReadDbContext<BillingDbContext> _readDbContext;

    public PaymentReportService(IReadDbContext<BillingDbContext> readDbContext)
    {
        _readDbContext = readDbContext;
    }

    public async Task<IReadOnlyList<PaymentReportRow>> GetPaymentsAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        List<PaymentReportRow> results = await _readDbContext.Context.Payments
            .Join(
                _readDbContext.Context.Invoices,
                p => p.InvoiceId,
                i => i.Id,
                (p, i) => new { Payment = p, Invoice = i })
            .Where(x => x.Payment.CreatedAt >= from && x.Payment.CreatedAt < to)
            .OrderByDescending(x => x.Payment.CreatedAt)
            .Select(x => new PaymentReportRow(
                x.Payment.Id.Value,
                x.Invoice.InvoiceNumber,
                x.Payment.Amount.Amount,
                x.Payment.Amount.Currency,
                x.Payment.Method.ToString(),
                x.Payment.Status.ToString(),
                x.Payment.CompletedAt))
            .ToListAsync(ct);

        return results;
    }
}
