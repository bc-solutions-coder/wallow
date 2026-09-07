using System.Diagnostics.CodeAnalysis;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// Shared URI syntax rules for client registration surfaces.
/// </summary>
public static class ClientUriRules
{
    public const string RedirectUriError =
        "Redirect URIs must be absolute, carry no fragment, and use https or http://localhost.";

    public const string LogoutUriError =
        "Logout URIs must be absolute http or https URIs without a fragment.";

    public const string BackchannelLogoutUriError =
        "Back-channel logout URIs must be absolute, carry no fragment, and use https; "
        + "plain http is allowed only for confidential clients.";

    /// <summary>
    /// A redirect (or post-logout redirect) URI: absolute, fragment-free, and either https or
    /// plain http to a loopback host so a developer can run the app locally.
    /// </summary>
    public static bool TryParseRedirectUri(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!TryParseWebUri(value, out Uri? parsed))
        {
            return false;
        }

        if (parsed.Scheme == Uri.UriSchemeHttp && !parsed.IsLoopback)
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>
    /// Accepts absolute, fragment-free HTTP or HTTPS logout URIs.
    /// This helper does not restrict hosts or require a private network.
    /// </summary>
    public static bool TryParseLogoutUri(string? value, [NotNullWhen(true)] out Uri? uri) =>
        TryParseWebUri(value, out uri);

    /// <summary>
    /// Accepts absolute, fragment-free back-channel logout URIs. HTTPS is required for
    /// public clients; confidential clients may also use HTTP.
    /// </summary>
    public static bool TryParseBackchannelLogoutUri(
        string? value,
        bool isConfidential,
        [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!TryParseWebUri(value, out Uri? parsed))
        {
            return false;
        }

        if (parsed.Scheme == Uri.UriSchemeHttp && !isConfidential)
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>Returns the first raw value <see cref="TryParseRedirectUri"/> refuses, or null.</summary>
    public static string? FirstRefusedRedirect(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.FirstOrDefault(v => !TryParseRedirectUri(v, out _));
    }

    private static bool TryParseWebUri(string? value, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
