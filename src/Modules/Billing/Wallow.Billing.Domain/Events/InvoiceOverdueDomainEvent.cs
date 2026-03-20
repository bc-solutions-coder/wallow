using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Events;

public sealed record InvoiceOverdueDomainEvent(
    Guid InvoiceId,
    Guid UserId,
    DateTime DueDate) : DomainEvent;
