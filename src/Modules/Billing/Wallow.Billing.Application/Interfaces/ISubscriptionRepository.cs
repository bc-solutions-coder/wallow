using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;

namespace Wallow.Billing.Application.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(SubscriptionId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Subscription?> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(Subscription subscription);
    void Update(Subscription subscription);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
