namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Registers a client on behalf of an organization. <c>Kind</c> is <c>application</c> for a
/// developer application a person signs in to or <c>service-account</c> for a client-credentials
/// client. Name and the derived client id are immutable once registered. An application needs at
/// least one redirect URI, absolute, fragment-free, and https or http://localhost; a service
/// account ignores every URI field. Both need at least one scope.
/// </summary>
public record RegisterOrganizationClientRequest(
    string Kind,
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string? BackchannelLogoutUri = null,
    RegisterOrganizationClientBranding? Branding = null);

/// <summary>
/// Optional initial branding for an application: the end-user-facing display name (defaults to
/// the client's name) and a tagline. Ignored for service accounts, which face no end user.
/// </summary>
public record RegisterOrganizationClientBranding(
    string? DisplayName = null,
    string? Tagline = null);

/// <summary>
/// Everything about a client its organization may change after registration. Name and client id
/// are immutable and deliberately absent.
/// </summary>
public record UpdateOrganizationClientRequest(
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string? BackchannelLogoutUri = null);

/// <summary>
/// Rotates a client's secret. <c>RevokeActiveTokens</c> additionally ends every token the client
/// was already issued, so a compromise response cuts every live session in the same step.
/// </summary>
public record RotateOrganizationClientSecretRequest(bool RevokeActiveTokens = false);
