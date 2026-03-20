namespace Wallow.Billing.Application.Metering.DTOs;

/// <summary>
/// Status of a quota for a tenant.
/// </summary>
public sealed record QuotaStatusDto(
    string MeterCode,
    string MeterDisplayName,
    decimal CurrentUsage,
    decimal Limit,
    decimal PercentUsed,
    string Period,
    string OnExceeded,
    bool IsOverride);
