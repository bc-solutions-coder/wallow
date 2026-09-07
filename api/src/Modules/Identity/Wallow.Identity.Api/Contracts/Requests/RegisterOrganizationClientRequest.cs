namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Organization client registration: application or service-account. Both require scopes.
/// Applications require an absolute, fragment-free HTTPS or loopback HTTP redirect;
/// service accounts ignore URI fields. Name and client id cannot be changed here after registration.
/// RefreshTokenLifetime is seconds for future application refresh tokens, defaulting to one day.
/// </summary>
public record RegisterOrganizationClientRequest(
    string Kind,
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string? BackchannelLogoutUri = null,
    bool BackchannelLogoutSessionRequired = false,
    RegisterOrganizationClientBranding? Branding = null,
    int? RefreshTokenLifetime = null,
    bool EnableObservability = false);

/// <summary>
/// Initial application display name and tagline. Omitted display name uses the client name;
/// service accounts ignore branding.
/// </summary>
public record RegisterOrganizationClientBranding(
    string? DisplayName = null,
    string? Tagline = null);

/// <summary>
/// Replaces application URIs, back-channel settings, and scopes. Null RefreshTokenLifetime
/// preserves the current lifetime; a value in seconds applies to future tokens.
/// </summary>
public record UpdateOrganizationClientRequest(
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string? BackchannelLogoutUri = null,
    bool BackchannelLogoutSessionRequired = false,
    int? RefreshTokenLifetime = null);

/// <summary>
/// Rotates the secret. RevokeActiveTokens also requests revocation of issued client tokens.
/// </summary>
public record RotateOrganizationClientSecretRequest(bool RevokeActiveTokens = false);
