using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.CancelSubscription;

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
