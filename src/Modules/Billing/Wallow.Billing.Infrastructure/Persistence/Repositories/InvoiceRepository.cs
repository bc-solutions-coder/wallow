using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository(BillingDbContext context) : IInvoiceRepository
{
    private static readonly Func<BillingDbContext, InvoiceId, CancellationToken, Task<Invoice?>>
        _getByIdQuery = EF.CompileAsyncQuery(
            (BillingDbContext ctx, InvoiceId id, CancellationToken _) =>
                ctx.Invoices
                    .AsTracking()
                    .FirstOrDefault(i => i.Id == id));

    public Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        return _getByIdQuery(context, id, cancellationToken);
    }

    public Task<Invoice?> GetByIdWithLineItemsAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        return context.Invoices
            .AsTracking()
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Invoices
            .Include(i => i.LineItems)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return context.Invoices
            .AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public void Add(Invoice invoice)
    {
        context.Invoices.Add(invoice);
    }

    public void Update(Invoice invoice)
    {
        context.Invoices.Update(invoice);
    }

    public void Remove(Invoice invoice)
    {
        context.Invoices.Remove(invoice);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
