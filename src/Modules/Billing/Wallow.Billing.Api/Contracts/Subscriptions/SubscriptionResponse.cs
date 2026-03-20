namespace Wallow.Billing.Api.Contracts.Subscriptions;

public sealed record SubscriptionResponse(
    Guid Id,
    Guid UserId,
    string PlanName,
    decimal Price,
    string Currency,
    string Status,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    DateTime? CancelledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
