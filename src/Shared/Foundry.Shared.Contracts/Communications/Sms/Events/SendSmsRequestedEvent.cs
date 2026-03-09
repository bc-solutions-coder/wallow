// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Communications.Sms.Events;

public sealed record SendSmsRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string To { get; init; }
    public required string Body { get; init; }
    public string? SourceModule { get; init; }
    public Guid? CorrelationId { get; init; }
}
