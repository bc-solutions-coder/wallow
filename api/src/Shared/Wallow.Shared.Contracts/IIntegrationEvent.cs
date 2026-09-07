namespace Wallow.Shared.Contracts;

/// <summary>
/// Public cross-module event contract. Use primitive IDs and simple DTOs.
/// Authoring rules: docs/architecture/messaging.md, Integration Events.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Supplies a new event ID and UTC occurrence time.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
