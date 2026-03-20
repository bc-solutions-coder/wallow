using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record SubscriptionCreatedDomainEvent(
    Guid SubscriptionId,
    Guid UserId,
    string PlanName,
    decimal Amount,
    string Currency) : DomainEvent;
