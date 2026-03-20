namespace Wallow.Billing.Application.Commands.CancelSubscription;

public sealed record CancelSubscriptionCommand(
    Guid SubscriptionId,
    Guid CancelledByUserId);
