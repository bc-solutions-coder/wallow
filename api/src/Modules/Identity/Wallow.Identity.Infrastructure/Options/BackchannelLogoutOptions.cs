namespace Wallow.Identity.Infrastructure.Options;

/// <summary>
/// Controls back-channel logout delivery. Private-network recipients require
/// <see cref="AllowPrivateNetworkHosts"/>.
/// </summary>
public sealed class BackchannelLogoutOptions
{
    public const string SectionName = "Identity:BackchannelLogout";

    /// <summary>
    /// Skips recipient address checks when true. Enable only when registered recipients may use private networks.
    /// </summary>
    public bool AllowPrivateNetworkHosts { get; set; }

    /// <summary>
    /// Timeout for each HTTP delivery attempt, excluding recipient lookup and token creation.
    /// </summary>
    public TimeSpan PerClientTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Delay before the single retry for a transport error, timeout, or server error.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Cancellation deadline for parallel deliveries after recipient lookup completes.
    /// </summary>
    public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
