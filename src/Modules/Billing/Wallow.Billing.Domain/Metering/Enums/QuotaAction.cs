namespace Wallow.Billing.Domain.Metering.Enums;

/// <summary>
/// Defines the action to take when a quota is exceeded.
/// </summary>
public enum QuotaAction
{
    /// <summary>
    /// Block the request with a 429 Too Many Requests response.
    /// </summary>
    Block = 0,

    /// <summary>
    /// Allow the request but add a warning header.
    /// </summary>
    Warn = 1,

    /// <summary>
    /// Slow down requests using rate limiting.
    /// </summary>
    Throttle = 2
}
