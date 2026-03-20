namespace Wallow.Billing.Application.Metering.Commands.RemoveQuotaOverride;

/// <summary>
/// Removes a tenant-specific quota override, reverting to plan defaults.
/// </summary>
public sealed record RemoveQuotaOverrideCommand(
    Guid TenantId,
    string MeterCode);
