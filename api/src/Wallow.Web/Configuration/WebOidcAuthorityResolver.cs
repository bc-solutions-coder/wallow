namespace Wallow.Web.Configuration;

/// <summary>
/// Resolves the OIDC authority this app's OpenID Connect client redirects the browser to.
/// </summary>
/// <remarks>
/// The authority must be the unified auth origin that reverse-proxies the OIDC endpoints, so it
/// matches the issuer advertised in the discovery document and baked into every token's iss
/// claim. It is browser-facing: a container-internal hostname belongs in Oidc:MetadataAddress.
/// </remarks>
public static class WebOidcAuthorityResolver
{
    /// <summary>The explicit authority override key.</summary>
    public const string AuthorityKey = "Oidc:Authority";

    /// <summary>The browser-facing unified auth origin key the authority falls back to.</summary>
    public const string AuthPublicUrlKey = "ServiceUrls:AuthPublicUrl";

    /// <summary>The legacy auth origin key, used only when no public origin is configured.</summary>
    public const string AuthUrlKey = "ServiceUrls:AuthUrl";

    /// <summary>
    /// Resolves the authority from configuration, preferring the explicit
    /// <see cref="AuthorityKey"/> override, then <see cref="AuthPublicUrlKey"/>, then
    /// <see cref="AuthUrlKey"/>.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The absolute authority origin, exactly as configured.</returns>
    /// <exception cref="InvalidOperationException">
    /// No key is configured, or a configured value is not an absolute http or https URI.
    /// </exception>
    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? explicitAuthority = configuration[AuthorityKey];
        if (!string.IsNullOrWhiteSpace(explicitAuthority))
        {
            return ValidateAbsolute(explicitAuthority, AuthorityKey);
        }

        string? authPublicUrl = configuration[AuthPublicUrlKey];
        if (!string.IsNullOrWhiteSpace(authPublicUrl))
        {
            return ValidateAbsolute(authPublicUrl, AuthPublicUrlKey);
        }

        string? authUrl = configuration[AuthUrlKey];
        if (!string.IsNullOrWhiteSpace(authUrl))
        {
            return ValidateAbsolute(authUrl, AuthUrlKey);
        }

        throw new InvalidOperationException(
            $"No OIDC authority is configured. Set '{AuthorityKey}', '{AuthPublicUrlKey}', or '{AuthUrlKey}' "
            + "to the browser-facing auth origin that proxies the OIDC endpoints.");
    }

    /// <summary>
    /// Validates the value is an absolute http or https URI and returns it as configured.
    /// </summary>
    /// <remarks>
    /// The value is returned rather than a <see cref="Uri"/> because the caller feeds it to
    /// UriBuilder and TrimEnd('/') for its endpoint rewrite; Uri normalization would append a
    /// trailing slash and silently change that logic.
    /// </remarks>
    private static string ValidateAbsolute(string value, string key)
    {
        string trimmed = value.Trim();

        // A leading-slash path parses as an absolute file:// URI on Unix, so the scheme has to
        // be checked explicitly to reject relative values such as "/connect".
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' must be an absolute http or https URI, but was '{value}'.");
        }

        return trimmed;
    }
}
