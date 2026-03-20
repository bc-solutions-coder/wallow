using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record SubscriptionCancelledDomainEvent(
    Guid SubscriptionId,
    Guid UserId,
    DateTime CancelledAt) : DomainEvent;
