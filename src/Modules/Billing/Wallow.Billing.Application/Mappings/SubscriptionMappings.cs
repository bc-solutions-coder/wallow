using Wallow.Billing.Application.DTOs;
using Wallow.Billing.Domain.Entities;

namespace Wallow.Billing.Application.Mappings;

public static class SubscriptionMappings
{
    public static SubscriptionDto ToDto(this Subscription subscription)
    {
        return new SubscriptionDto(
            Id: subscription.Id.Value,
            UserId: subscription.UserId,
            PlanName: subscription.PlanName,
            Price: subscription.Price.Amount,
            Currency: subscription.Price.Currency,
            Status: subscription.Status.ToString(),
            StartDate: subscription.StartDate,
            EndDate: subscription.EndDate,
            CurrentPeriodStart: subscription.CurrentPeriodStart,
            CurrentPeriodEnd: subscription.CurrentPeriodEnd,
            CancelledAt: subscription.CancelledAt,
            CreatedAt: subscription.CreatedAt,
            UpdatedAt: subscription.UpdatedAt,
            CustomFields: subscription.CustomFields);
    }
}
