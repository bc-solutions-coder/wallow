using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record InvoicePaidDomainEvent(
    Guid InvoiceId,
    Guid PaymentId,
    DateTime PaidAt) : DomainEvent;
