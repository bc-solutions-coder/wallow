namespace Wallow.Shared.Contracts;

/// <summary>
/// Marker interface for integration events.
/// Integration events are published to the message broker for cross-module communication.
/// Unlike domain events (internal to a module), integration events are the public contract.
///
/// Design notes:
/// - Use plain Guid for IDs, not strongly-typed IDs (simpler serialization)
/// - Use primitive types and simple DTOs only (no domain entities)
/// - Events describe what happened (past tense), not what should happen
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base record for integration events with default implementations.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
