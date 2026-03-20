using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetSubscriptionsByUserId;

public sealed class GetSubscriptionsByUserIdHandler(ISubscriptionRepository subscriptionRepository)
{
    public async Task<Result<IReadOnlyList<SubscriptionDto>>> Handle(
        GetSubscriptionsByUserIdQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Subscription> subscriptions = await subscriptionRepository.GetByUserIdAsync(query.UserId, cancellationToken);
        List<SubscriptionDto> dtos = subscriptions.Select(s => s.ToDto()).ToList();
        return Result.Success<IReadOnlyList<SubscriptionDto>>(dtos);
    }
}
