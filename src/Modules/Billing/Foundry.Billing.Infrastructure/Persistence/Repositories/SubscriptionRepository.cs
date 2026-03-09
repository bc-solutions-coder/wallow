using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Enums;
using Foundry.Billing.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Foundry.Billing.Infrastructure.Persistence.Repositories;

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

    public async Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Subscriptions
            .OrderByDescending(s => s.CreatedAt)
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
