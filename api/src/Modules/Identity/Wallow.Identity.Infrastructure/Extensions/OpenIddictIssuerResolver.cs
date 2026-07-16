using Microsoft.Extensions.Configuration;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Resolves the issuer OpenIddict advertises in its discovery document and bakes into the
/// iss claim of every token it mints.
/// </summary>
/// <remarks>
/// The OIDC endpoints are reached through the unified auth origin's reverse proxy, so the
/// issuer must be that public origin rather than the API's own request origin.
/// </remarks>
public static class OpenIddictIssuerResolver
{
    /// <summary>The explicit issuer override key.</summary>
    public const string IssuerKey = "OpenIddict:Issuer";

    /// <summary>The unified auth origin key the issuer falls back to.</summary>
    public const string AuthUrlKey = "AuthUrl";

    /// <summary>
    /// Resolves the issuer from configuration, preferring the explicit
    /// <see cref="IssuerKey"/> override and falling back to <see cref="AuthUrlKey"/>.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>
    /// The absolute issuer URI, or <see langword="null"/> when neither key is configured
    /// (leaving OpenIddict to derive the issuer from the request origin).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// A configured value is not an absolute URI.
    /// </exception>
    public static Uri? Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? explicitIssuer = configuration[IssuerKey];
        if (!string.IsNullOrWhiteSpace(explicitIssuer))
        {
            return ParseAbsolute(explicitIssuer, IssuerKey);
        }

        string? authUrl = configuration[AuthUrlKey];
        if (!string.IsNullOrWhiteSpace(authUrl))
        {
            return ParseAbsolute(authUrl, AuthUrlKey);
        }

        return null;
    }

    private static Uri ParseAbsolute(string value, string key)
    {
        // A leading-slash path parses as an absolute file:// URI on Unix, so the scheme has to
        // be checked explicitly to reject relative values such as "/connect".
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' must be an absolute http or https URI, but was '{value}'.");
        }

        return uri;
    }
}
