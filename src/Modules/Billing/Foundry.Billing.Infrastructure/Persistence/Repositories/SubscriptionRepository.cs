using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly BillingDbContext _context;

    public SubscriptionRepository(BillingDbContext context)
    {
        _context = context;
    }

    public Task<Subscription?> GetByIdAsync(SubscriptionId id, CancellationToken cancellationToken = default)
    {
        return _context.Subscriptions
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Subscriptions
            .AsTracking()
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(Subscription subscription)
    {
        _context.Subscriptions.Add(subscription);
    }

    public void Update(Subscription subscription)
    {
        _context.Subscriptions.Update(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
