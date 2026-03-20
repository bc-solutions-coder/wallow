namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// Marker interface for domain events. Domain events represent something
/// that happened in the domain that other parts of the system may need to know about.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// When the event occurred (UTC).
    /// </summary>
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base record for domain events with default implementations.
/// Use records for immutability and value-based equality.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
