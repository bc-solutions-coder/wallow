namespace Wallow.Billing.Domain.Metering.Enums;

/// <summary>
/// Defines how meter values are aggregated over a period.
/// </summary>
public enum MeterAggregation
{
    /// <summary>
    /// Sum all values in the period (e.g., total API calls).
    /// </summary>
    Sum = 0,

    /// <summary>
    /// Take the maximum value in the period (e.g., peak storage used).
    /// </summary>
    Max = 1
}
