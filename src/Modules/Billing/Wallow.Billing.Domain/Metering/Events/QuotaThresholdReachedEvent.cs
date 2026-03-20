using Wallow.Shared.Kernel.Domain;

namespace Wallow.Billing.Domain.Metering.Events;

/// <summary>
/// Raised when usage reaches a quota threshold (e.g., 80%, 90%, 100%).
/// Used to trigger notifications and logging.
/// </summary>
public sealed record QuotaThresholdReachedEvent(
    Guid TenantId,
    string MeterCode,
    string MeterDisplayName,
    decimal CurrentUsage,
    decimal Limit,
    int PercentUsed) : DomainEvent;
