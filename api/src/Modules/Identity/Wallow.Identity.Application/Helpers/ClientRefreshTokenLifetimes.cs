using System.Globalization;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// The registration-time refresh-token lifetime policy, in seconds — the unit the API speaks.
/// Every registration surface (seed sync, the platform admin endpoint, the organization
/// endpoint) writes an explicit per-client lifetime when the caller supplies none, so a client
/// row always shows its policy; only rows created before the policy existed fall back to the
/// global <c>OpenIddict:RefreshTokenLifetimeDays</c> configuration.
/// </summary>
/// <remarks>
/// The names live here, below both the Api layer and the Infrastructure layer, for the same
/// reason as <see cref="ClientApplicationProperties"/>: both layers write descriptors and
/// cannot see each other, and two spellings of a default is a policy that silently forks.
/// </remarks>
public static class ClientRefreshTokenLifetimes
{
    /// <summary>Seven days, for the platform's own (first-party) clients.</summary>
    public const int FirstPartyDefaultSeconds = 604_800;

    /// <summary>One day, for every client registered by an organization or an administrator.</summary>
    public const int ThirdPartyDefaultSeconds = 86_400;

    /// <summary>Below a minute a client cannot complete a single refresh cycle reliably.</summary>
    public const int MinimumSeconds = 60;

    /// <summary>One year. A longer-lived refresh token is a standing credential, not a session.</summary>
    public const int MaximumSeconds = 31_536_000;

    /// <summary>The one message every surface returns for an out-of-range lifetime.</summary>
    public static readonly string RangeMessage =
        $"Refresh token lifetime must be between {MinimumSeconds} and {MaximumSeconds} seconds.";

    /// <summary>Whether a caller-supplied lifetime falls inside the accepted range.</summary>
    public static bool IsInRange(int seconds)
    {
        return seconds is >= MinimumSeconds and <= MaximumSeconds;
    }

    /// <summary>
    /// The OpenIddict per-application setting value for a lifetime: an invariant-culture
    /// <see cref="TimeSpan"/> string. The format lives here so the Api and Infrastructure
    /// descriptor extensions cannot drift apart.
    /// </summary>
    public static string ToSettingValue(int seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString("c", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a stored setting value back to whole seconds, or <see langword="null"/> when the
    /// value is absent or unreadable.
    /// </summary>
    public static int? FromSettingValue(string? setting)
    {
        return TimeSpan.TryParse(setting, CultureInfo.InvariantCulture, out TimeSpan lifetime)
            ? (int)lifetime.TotalSeconds
            : null;
    }
}
