namespace Wallow.Billing.Domain.Metering.Enums;

/// <summary>
/// Defines the time period over which quota limits are evaluated.
/// </summary>
public enum QuotaPeriod
{
    /// <summary>
    /// Quota resets every hour.
    /// </summary>
    Hourly = 0,

    /// <summary>
    /// Quota resets daily at midnight UTC.
    /// </summary>
    Daily = 1,

    /// <summary>
    /// Quota resets on the first of each month.
    /// </summary>
    Monthly = 2
}
