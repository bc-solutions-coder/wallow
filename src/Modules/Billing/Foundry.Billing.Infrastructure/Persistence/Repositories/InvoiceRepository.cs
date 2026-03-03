using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly BillingDbContext _context;

    public InvoiceRepository(BillingDbContext context)
    {
        _context = context;
    }

    public Task<Invoice?> GetByIdAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        return _context.Invoices
            .AsTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public Task<Invoice?> GetByIdWithLineItemsAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        return _context.Invoices
            .AsTracking()
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Invoices
            .Include(i => i.LineItems)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        return _context.Invoices
            .AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public void Add(Invoice invoice)
    {
        _context.Invoices.Add(invoice);
    }

    public void Update(Invoice invoice)
    {
        _context.Invoices.Update(invoice);
    }

    public void Remove(Invoice invoice)
    {
        _context.Invoices.Remove(invoice);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
