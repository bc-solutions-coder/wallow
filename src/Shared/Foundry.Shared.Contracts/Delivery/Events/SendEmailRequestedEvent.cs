// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Delivery.Events;

public sealed record SendEmailRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string To { get; init; }
    public string? From { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? SourceModule { get; init; }
    public Guid? CorrelationId { get; init; }
    public required bool IsCritical { get; init; }
}
