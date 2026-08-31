namespace Wallow.Identity.Infrastructure.Options;

/// <summary>
/// Tuning for OIDC back-channel logout delivery. The defaults suit relying parties on the public
/// internet; a deployment whose relying parties live on a private network (local dev, e2e,
/// air-gapped installs) must opt in via <see cref="AllowPrivateNetworkHosts"/>.
/// </summary>
public sealed class BackchannelLogoutOptions
{
    public const string SectionName = "Identity:BackchannelLogout";

    /// <summary>
    /// Whether logout tokens may be POSTed to loopback, RFC 1918, link-local, or unique-local
    /// hosts. Off by default: back-channel URIs are attacker-registrable (any org admin sets
    /// one), so without this knob a registration pointing at an internal service would turn
    /// every logout into a server-side request against it.
    /// </summary>
    public bool AllowPrivateNetworkHosts { get; set; }

    /// <summary>How long one delivery attempt to one relying party may take.</summary>
    public TimeSpan PerClientTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>The pause before the single retry a failed delivery gets.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The bound on the whole notification fan-out. Deliveries run in parallel, so this is a
    /// backstop for many slow relying parties, not a per-client budget.
    /// </summary>
    public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
