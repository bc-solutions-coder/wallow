namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>Durable notification to reconcile current desired state. Contains no credential material.</summary>
public sealed record TelemetryDesiredChangedEvent : IntegrationEvent
{
    public required Guid RegistrationId { get; init; }
}
