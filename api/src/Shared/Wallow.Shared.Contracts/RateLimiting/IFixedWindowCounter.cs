namespace Wallow.Shared.Contracts.RateLimiting;

/// <summary>
/// Counts attempts in a window starting with the first increment. Later increments do not
/// refresh the expiry. Callers own thresholds and handling of storage failures.
/// </summary>
public interface IFixedWindowCounter
{
    Task<long> IncrementAsync(string key, TimeSpan window);

    /// <summary>Returns the positive remaining TTL, or the window when none remains.</summary>
    Task<TimeSpan> GetRetryAfterAsync(string key, TimeSpan window);
}
