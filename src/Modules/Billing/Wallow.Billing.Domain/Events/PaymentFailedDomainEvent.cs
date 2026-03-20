using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record PaymentFailedDomainEvent(
    Guid PaymentId,
    Guid InvoiceId,
    string Reason,
    Guid UserId) : DomainEvent;
