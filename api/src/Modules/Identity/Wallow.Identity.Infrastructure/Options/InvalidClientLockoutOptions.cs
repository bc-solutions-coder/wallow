namespace Wallow.Identity.Infrastructure.Options;

/// <summary>
/// Failure-counting window, rejection threshold, and lockout duration for client authentication.
/// </summary>
public sealed class InvalidClientLockoutOptions
{
    public const string SectionName = "Identity:InvalidClientLockout";

    /// <summary>Failed authentications within the window that trip the lockout.</summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>How long the failure counter lives before it resets.</summary>
    public int WindowMinutes { get; set; } = 5;

    /// <summary>
    /// Lockout duration written when the failure count meets or exceeds the threshold.
    /// </summary>
    public int LockoutMinutes { get; set; } = 5;
}
