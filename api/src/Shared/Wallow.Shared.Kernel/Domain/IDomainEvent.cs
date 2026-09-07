namespace Wallow.Shared.Kernel.Domain;

/// <summary>
/// A fact raised within a module's domain.
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
/// Supplies a new event ID and UTC occurrence time.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
