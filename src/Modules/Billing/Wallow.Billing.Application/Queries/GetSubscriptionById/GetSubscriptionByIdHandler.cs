using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Queries.GetSubscriptionById;

public sealed class GetSubscriptionByIdHandler(ISubscriptionRepository subscriptionRepository)
{
    public async Task<Result<SubscriptionDto>> Handle(
        GetSubscriptionByIdQuery query,
        CancellationToken cancellationToken)
    {
        SubscriptionId subscriptionId = SubscriptionId.Create(query.SubscriptionId);
        Subscription? subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(Error.NotFound("Subscription", query.SubscriptionId));
        }

        return Result.Success(subscription.ToDto());
    }
}
