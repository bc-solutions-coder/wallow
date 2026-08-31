namespace Wallow.Shared.Contracts.Branding;

/// <summary>
/// A client's branding as a sign-in screen renders it: the public copy of the row the owning
/// organization edits, with the logo already resolved to a fetchable URL.
/// </summary>
public sealed record PublicClientBranding(
    string ClientId,
    string DisplayName,
    string? Tagline,
    string? LogoUrl,
    string? ThemeJson);

/// <summary>
/// Branding's public read for other modules. Identity's authorize-context endpoint dresses the
/// auth host's transaction screens through this contract — never through Branding's persistence,
/// per the module isolation rules.
/// </summary>
public interface IClientBrandingProvider
{
    /// <summary>The client's branding, or <see langword="null"/> when no row exists for it.</summary>
    Task<PublicClientBranding?> FindAsync(string clientId, CancellationToken ct = default);
}
