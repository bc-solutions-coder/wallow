using System.Globalization;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Shared registration defaults, validation bounds and setting conversion for refresh-token
/// lifetimes in seconds. Clients without a stored setting use the global OpenIddict fallback.
/// </summary>
public static class ClientRefreshTokenLifetimes
{
    /// <summary>Seven days, for the platform's own (first-party) clients.</summary>
    public const int FirstPartyDefaultSeconds = 604_800;

    /// <summary>
    /// One day, the default for third-party clients with refresh-token lifetimes.
    /// </summary>
    public const int ThirdPartyDefaultSeconds = 86_400;

    /// <summary>
    /// Minimum accepted per-client lifetime: one minute.
    /// </summary>
    public const int MinimumSeconds = 60;

    /// <summary>
    /// Maximum accepted per-client lifetime: one year.
    /// </summary>
    public const int MaximumSeconds = 31_536_000;

    /// <summary>
    /// Validation message for lifetimes outside the accepted range.
    /// </summary>
    public static readonly string RangeMessage =
        $"Refresh token lifetime must be between {MinimumSeconds} and {MaximumSeconds} seconds.";

    /// <summary>
    /// Whether the lifetime is within the inclusive bounds.
    /// </summary>
    public static bool IsInRange(int seconds)
    {
        return seconds is >= MinimumSeconds and <= MaximumSeconds;
    }

    /// <summary>
    /// Formats seconds as an invariant <see cref="TimeSpan"/> setting shared by both descriptor adapters.
    /// </summary>
    public static string ToSettingValue(int seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString("c", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a setting and truncates fractional seconds; returns null when absent or unparseable.
    /// </summary>
    public static int? FromSettingValue(string? setting)
    {
        return TimeSpan.TryParse(setting, CultureInfo.InvariantCulture, out TimeSpan lifetime)
            ? (int)lifetime.TotalSeconds
            : null;
    }
}
