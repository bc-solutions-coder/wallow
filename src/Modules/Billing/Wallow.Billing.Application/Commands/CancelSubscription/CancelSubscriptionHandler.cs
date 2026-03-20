using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Commands.CancelSubscription;

public sealed class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<SubscriptionDto>> Handle(
        CancelSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        SubscriptionId subscriptionId = SubscriptionId.Create(command.SubscriptionId);
        Subscription? subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return Result.Failure<SubscriptionDto>(
                Error.NotFound("Subscription", command.SubscriptionId));
        }

        subscription.Cancel(command.CancelledByUserId, timeProvider);
        await subscriptionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(subscription.ToDto());
    }
}
