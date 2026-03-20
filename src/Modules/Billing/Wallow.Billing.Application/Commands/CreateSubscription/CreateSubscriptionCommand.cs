namespace Wallow.Billing.Application.Commands.CreateSubscription;

public sealed record CreateSubscriptionCommand(
    Guid UserId,
    string PlanName,
    decimal Price,
    string Currency,
    DateTime StartDate,
    DateTime PeriodEnd,
    Dictionary<string, object>? CustomFields = null);
