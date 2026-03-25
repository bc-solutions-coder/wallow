using Microsoft.EntityFrameworkCore;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Kernel.Persistence;

namespace Wallow.Billing.Infrastructure.Services;

public sealed class InvoiceReportService : IInvoiceReportService
{
    private readonly IReadDbContext<BillingDbContext> _readDbContext;

    public InvoiceReportService(IReadDbContext<BillingDbContext> readDbContext)
    {
        _readDbContext = readDbContext;
    }

    public async Task<IReadOnlyList<InvoiceReportRow>> GetInvoicesAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        List<InvoiceReportRow> results = await _readDbContext.Context.Invoices
            .Where(i => i.Status != InvoiceStatus.Draft)
            .Where(i => i.CreatedAt >= from && i.CreatedAt < to)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceReportRow(
                i.InvoiceNumber,
                "User_" + i.UserId,
                i.TotalAmount.Amount,
                i.TotalAmount.Currency,
                i.Status.ToString(),
                i.CreatedAt,
                i.DueDate))
            .ToListAsync(ct);

        return results;
    }
}
