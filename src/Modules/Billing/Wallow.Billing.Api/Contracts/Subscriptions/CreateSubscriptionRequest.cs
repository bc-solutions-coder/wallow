namespace Wallow.Billing.Api.Contracts.Subscriptions;

public sealed record CreateSubscriptionRequest(
    string PlanName,
    decimal Price,
    string Currency,
    DateTime StartDate,
    DateTime PeriodEnd);
