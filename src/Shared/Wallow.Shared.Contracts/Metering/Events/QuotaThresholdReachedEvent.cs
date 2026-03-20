namespace Wallow.Shared.Contracts.Metering.Events;

/// <summary>
/// Published when a tenant's usage reaches a quota threshold (e.g., 80%, 90%, 100%).
/// Consumers: Communications (alert user), Billing (potential overage charges)
/// </summary>
public sealed record QuotaThresholdReachedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string MeterCode { get; init; }
    public required string MeterDisplayName { get; init; }
    public required decimal CurrentUsage { get; init; }
    public required decimal Limit { get; init; }
    public required int PercentUsed { get; init; }
    public required string Period { get; init; }
}
