using Wallow.Billing.Domain.Metering.Enums;

namespace Wallow.Billing.Application.Metering.Commands.SetQuotaOverride;

/// <summary>
/// Sets a quota override for a specific tenant.
/// </summary>
public sealed record SetQuotaOverrideCommand(
    Guid TenantId,
    string MeterCode,
    decimal Limit,
    QuotaPeriod Period,
    QuotaAction OnExceeded);
