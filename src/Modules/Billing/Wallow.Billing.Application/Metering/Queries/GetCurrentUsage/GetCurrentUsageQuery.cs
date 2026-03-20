using Wallow.Billing.Domain.Metering.Enums;

namespace Wallow.Billing.Application.Metering.Queries.GetCurrentUsage;

/// <summary>
/// Gets the current usage for the tenant, optionally filtered by meter and period.
/// </summary>
public sealed record GetCurrentUsageQuery(
    string? MeterCode = null,
    QuotaPeriod? Period = null);
