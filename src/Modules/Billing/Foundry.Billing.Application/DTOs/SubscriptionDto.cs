namespace Foundry.Billing.Application.DTOs;

public sealed record SubscriptionDto(
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
    DateTimeOffset? UpdatedAt,
    Dictionary<string, object>? CustomFields);
