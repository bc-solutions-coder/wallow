using Wallow.Billing.Domain.Metering.Enums;

namespace Wallow.Billing.Application.Metering.DTOs;

/// <summary>
/// Result of a quota check operation.
/// </summary>
/// <param name="IsAllowed">Whether the operation is allowed based on quota.</param>
/// <param name="CurrentUsage">Current usage value.</param>
/// <param name="Limit">Quota limit.</param>
/// <param name="PercentUsed">Percentage of quota used (0-100+).</param>
/// <param name="ActionIfExceeded">Action to take if exceeded (null if not exceeded).</param>
public sealed record QuotaCheckResult(
    bool IsAllowed,
    decimal CurrentUsage,
    decimal Limit,
    decimal PercentUsed,
    QuotaAction? ActionIfExceeded)
{
    /// <summary>
    /// Creates a result indicating no quota is configured.
    /// </summary>
    public static QuotaCheckResult Unlimited => new(true, 0, decimal.MaxValue, 0, null);
}
