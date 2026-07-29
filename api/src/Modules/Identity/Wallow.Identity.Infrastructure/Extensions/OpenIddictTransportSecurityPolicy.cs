using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Decides whether OpenIddict may serve its endpoints over plain HTTP.
/// </summary>
/// <remarks>
/// OpenIddict rejects non-HTTPS requests to its endpoints unless the transport security
/// requirement is disabled. Disabling it unconditionally means a misconfigured deployment
/// silently serves the authorization and token endpoints in the clear, so it is off by
/// default outside local development and has to be opted into explicitly.
/// </remarks>
public static class OpenIddictTransportSecurityPolicy
{
    /// <summary>
    /// The configuration key a deployment sets to allow plain-HTTP OpenIddict endpoints
    /// outside development, for example when TLS terminates at a reverse proxy and
    /// container-to-container discovery calls never leave the private network.
    /// </summary>
    public const string AllowPlainHttpKey = "OpenIddict:AllowPlainHttpEndpoints";

    /// <summary>The environment name the in-process test host runs under.</summary>
    public const string TestingEnvironmentName = "Testing";

    /// <summary>
    /// Determines whether the OpenIddict transport security requirement should be disabled.
    /// </summary>
    /// <param name="environment">The host environment.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>
    /// <see langword="true"/> when OpenIddict may answer plain-HTTP requests; otherwise
    /// <see langword="false"/>, leaving OpenIddict to require HTTPS.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="environment"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The configured <see cref="AllowPlainHttpKey"/> value is not a boolean.
    /// </exception>
    public static bool ShouldDisableTransportSecurityRequirement(
        IHostEnvironment environment, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        // Neither the local host nor the in-process test host has a certificate to serve,
        // so they always run the OIDC endpoints over plain HTTP.
        if (environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironmentName))
        {
            return true;
        }

        string? optIn = configuration[AllowPlainHttpKey];
        if (string.IsNullOrWhiteSpace(optIn))
        {
            return false;
        }

        // GetValue reports a malformed value as an InvalidOperationException naming the key
        // path, so a typo fails startup instead of silently resolving either way.
        return configuration.GetValue<bool>(AllowPlainHttpKey);
    }
}
