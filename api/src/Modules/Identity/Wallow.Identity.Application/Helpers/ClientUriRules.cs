using System.Diagnostics.CodeAnalysis;

namespace Wallow.Identity.Application.Helpers;

/// <summary>
/// The one rule set for the URIs a client registers, shared by the org-scoped client surface, the
/// platform admin surface and seed sync so no path can register a redirect the others refuse.
/// </summary>
public static class ClientUriRules
{
    public const string RedirectUriError =
        "Redirect URIs must be absolute, carry no fragment, and use https or http://localhost.";

    public const string LogoutUriError =
        "Logout URIs must be absolute http or https URIs without a fragment.";

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
    /// A front- or back-channel logout URI: absolute, fragment-free, http or https. Plain http is
    /// tolerated because the identity provider calls it server-to-server on a private network.
    /// </summary>
    public static bool TryParseLogoutUri(string? value, [NotNullWhen(true)] out Uri? uri) =>
        TryParseWebUri(value, out uri);

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
