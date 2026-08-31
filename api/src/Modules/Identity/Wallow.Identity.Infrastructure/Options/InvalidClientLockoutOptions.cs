namespace Wallow.Identity.Infrastructure.Options;

/// <summary>
/// How many failed client authentications a client_id may accumulate before the token endpoint
/// temporarily rejects it, and for how long. The counting window and the lockout are both fixed
/// windows, deliberately: a sliding window would let a slow, patient guesser keep a client locked
/// forever, while a fixed one guarantees the legitimate owner gets the client back.
/// </summary>
public sealed class InvalidClientLockoutOptions
{
    public const string SectionName = "Identity:InvalidClientLockout";

    /// <summary>Failed authentications within the window that trip the lockout.</summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>How long the failure counter lives before it resets.</summary>
    public int WindowMinutes { get; set; } = 5;

    /// <summary>How long a tripped client stays rejected.</summary>
    public int LockoutMinutes { get; set; } = 5;
}
