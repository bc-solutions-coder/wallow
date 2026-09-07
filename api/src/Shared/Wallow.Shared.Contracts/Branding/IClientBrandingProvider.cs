namespace Wallow.Shared.Contracts.Branding;

/// <summary>
/// Public client branding with the logo resolved to a fetchable URL.
/// </summary>
public sealed record PublicClientBranding(
    string ClientId,
    string DisplayName,
    string? Tagline,
    string? LogoUrl,
    string? ThemeJson);

/// <summary>
/// Cross-module branding reads used by Identity's authorize-context endpoint.
/// </summary>
public interface IClientBrandingProvider
{
    /// <summary>The client's branding, or <see langword="null"/> when no row exists for it.</summary>
    Task<PublicClientBranding?> FindAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Reads the current display name without the public-read cache; returns <see langword="null"/>
    /// when absent. Use this for synchronization after <c>ClientBrandingUpdatedEvent</c>
    /// so reordered deliveries do not apply stale payloads.
    /// </summary>
    Task<string?> FindCurrentDisplayNameAsync(string clientId, CancellationToken ct = default);
}
