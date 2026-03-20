using Wallow.Billing.Application.Metering.DTOs;
using Wallow.Billing.Domain.Metering.Enums;

namespace Wallow.Billing.Application.Metering.Services;

/// <summary>
/// Core metering service for tracking usage and checking quotas.
/// </summary>
public interface IMeteringService
{
    /// <summary>
    /// Increment a meter counter. Called by middleware or directly for specific operations.
    /// </summary>
    /// <param name="meterCode">The meter to increment (e.g., "api.calls")</param>
    /// <param name="value">The value to add (default 1)</param>
    /// <param name="dimensions">Optional dimensions for more granular tracking</param>
    Task IncrementAsync(string meterCode, decimal value = 1, Dictionary<string, string>? dimensions = null);

    /// <summary>
    /// Check if the current tenant is within quota for a meter.
    /// </summary>
    /// <param name="meterCode">The meter to check</param>
    /// <returns>Result indicating if allowed and quota status</returns>
    Task<QuotaCheckResult> CheckQuotaAsync(string meterCode);

    /// <summary>
    /// Get current usage for a meter in the current period.
    /// Reads from Valkey for real-time data.
    /// </summary>
    /// <param name="meterCode">The meter to query</param>
    /// <param name="period">The period to query</param>
    /// <returns>Current usage value</returns>
    Task<decimal> GetCurrentUsageAsync(string meterCode, QuotaPeriod period);

    /// <summary>
    /// Get historical usage records for a meter.
    /// Reads from PostgreSQL for persisted data.
    /// </summary>
    /// <param name="meterCode">The meter to query</param>
    /// <param name="from">Start date (inclusive)</param>
    /// <param name="to">End date (exclusive)</param>
    /// <returns>List of usage records</returns>
    Task<IReadOnlyList<UsageRecordDto>> GetUsageHistoryAsync(string meterCode, DateTime from, DateTime to);
}
