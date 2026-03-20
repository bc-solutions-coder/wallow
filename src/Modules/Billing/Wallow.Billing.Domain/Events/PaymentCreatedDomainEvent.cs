using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record PaymentCreatedDomainEvent(
    Guid PaymentId,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    Guid UserId) : DomainEvent;
