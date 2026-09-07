using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Wallow.Identity.Infrastructure.Extensions;

/// <summary>
/// Allows plain HTTP in Development and Testing, or when explicitly enabled in configuration.
/// </summary>
public static class OpenIddictTransportSecurityPolicy
{
    /// <summary>
    /// Configuration key for allowing plain HTTP outside Development and Testing.
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

        // Local and in-process test hosts may use HTTP.
        if (environment.IsDevelopment() || environment.IsEnvironment(TestingEnvironmentName))
        {
            return true;
        }

        string? optIn = configuration[AllowPlainHttpKey];
        if (string.IsNullOrWhiteSpace(optIn))
        {
            return false;
        }

        // Invalid boolean configuration fails instead of silently allowing HTTP.
        return configuration.GetValue<bool>(AllowPlainHttpKey);
    }
}
