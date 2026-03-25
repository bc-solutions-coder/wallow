using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Enums;
using Wallow.Billing.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Billing.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository(BillingDbContext context) : ISubscriptionRepository
{
    private static readonly Func<BillingDbContext, SubscriptionId, CancellationToken, Task<Subscription?>>
        _getByIdQuery = EF.CompileAsyncQuery(
            (BillingDbContext ctx, SubscriptionId id, CancellationToken _) =>
                ctx.Subscriptions
                    .AsTracking()
                    .FirstOrDefault(s => s.Id == id));

    public Task<Subscription?> GetByIdAsync(SubscriptionId id, CancellationToken cancellationToken = default)
    {
        return _getByIdQuery(context, id, cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetAllAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        return await context.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return context.Subscriptions
            .AsTracking()
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
    {
        return context.Subscriptions.CountAsync(cancellationToken);
    }

    public void Add(Subscription subscription)
    {
        context.Subscriptions.Add(subscription);
    }

    public void Update(Subscription subscription)
    {
        context.Subscriptions.Update(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}
