using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Mappings;
using Wallow.Billing.Domain.Entities;
using Wallow.Billing.Domain.ValueObjects;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Billing.Application.Commands.CreateSubscription;

public sealed class CreateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    TimeProvider timeProvider)
{
    public async Task<Result<SubscriptionDto>> Handle(
        CreateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        Money price = Money.Create(command.Price, command.Currency);
        Subscription subscription = Subscription.Create(
            command.UserId,
            command.PlanName,
            price,
            command.StartDate,
            command.PeriodEnd,
            command.UserId,
            timeProvider,
            command.CustomFields);

        subscriptionRepository.Add(subscription);
        await subscriptionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(subscription.ToDto());
    }
}
