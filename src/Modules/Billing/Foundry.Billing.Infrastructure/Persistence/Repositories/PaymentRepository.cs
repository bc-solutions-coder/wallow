using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Kernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly BillingDbContext _context;

    public PaymentRepository(BillingDbContext context)
    {
        _context = context;
    }

    public Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default)
    {
        return _context.Payments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Payment>> GetByInvoiceIdPagedAsync(InvoiceId invoiceId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<Payment> query = _context.Payments
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Payment> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Payment>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Payment>> GetByUserIdPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<Payment> query = _context.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Payment> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Payment>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Payments
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Payment>> GetAllPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<Payment> query = _context.Payments
            .OrderByDescending(p => p.CreatedAt);

        int totalCount = await query.CountAsync(cancellationToken);
        List<Payment> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Payment>(items, totalCount, page, pageSize);
    }

    public void Add(Payment payment)
    {
        _context.Payments.Add(payment);
    }

    public void Update(Payment payment)
    {
        _context.Payments.Update(payment);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
