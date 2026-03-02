using Foundry.Billing.Application.DTOs;
using Foundry.Billing.Application.Interfaces;
using Foundry.Billing.Application.Mappings;
using Foundry.Billing.Domain.Entities;
using Foundry.Billing.Domain.ValueObjects;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Billing.Application.Commands.CreateSubscription;

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
