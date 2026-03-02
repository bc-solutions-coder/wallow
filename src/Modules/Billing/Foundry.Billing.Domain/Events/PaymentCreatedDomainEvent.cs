using Foundry.Shared.Kernel.Domain;

namespace Foundry.Billing.Domain.Events;

public sealed record PaymentCreatedDomainEvent(
    Guid PaymentId,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    Guid UserId) : DomainEvent;
