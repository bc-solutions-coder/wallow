namespace Foundry.Shared.Contracts.Metering.Events;

/// <summary>
/// Published when usage data has been flushed from Valkey to PostgreSQL.
/// Consumers: Billing (generate usage-based invoice line items)
/// </summary>
public sealed record UsageFlushedEvent : IntegrationEvent
{
    public required DateTime FlushedAt { get; init; }
    public required int RecordCount { get; init; }
}
