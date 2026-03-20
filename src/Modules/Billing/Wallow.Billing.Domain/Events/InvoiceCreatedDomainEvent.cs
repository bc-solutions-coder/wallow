using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record InvoiceCreatedDomainEvent(
    Guid InvoiceId,
    Guid UserId,
    decimal TotalAmount,
    string Currency) : DomainEvent;
