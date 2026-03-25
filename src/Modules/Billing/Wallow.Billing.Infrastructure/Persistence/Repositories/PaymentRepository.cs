using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository(BillingDbContext context) : IPaymentRepository
{

    public Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default)
    {
        return context.Payments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(InvoiceId invoiceId, CancellationToken cancellationToken = default)
    {
        return await context.Payments
            .Where(p => p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Payments
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Payment>> GetAllAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await context.Payments
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
    {
        return context.Payments.CountAsync(cancellationToken);
    }

    public void Add(Payment payment)
    {
        context.Payments.Add(payment);
    }

    public void Update(Payment payment)
    {
        context.Payments.Update(payment);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
