using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Metering.Events;

/// <summary>
/// Raised when usage has been flushed from Valkey to PostgreSQL.
/// Allows the Billing module to process usage for invoicing.
/// </summary>
public sealed record UsageFlushedEvent(
    DateTime FlushedAt,
    int RecordCount) : DomainEvent;
