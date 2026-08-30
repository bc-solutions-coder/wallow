namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Registers a client on behalf of an organization. <c>Kind</c> is <c>application</c> for a
/// developer application a person signs in to (the only kind this surface registers today) or
/// <c>service-account</c>. Name and the derived client id are immutable once registered. Redirect
/// URIs must be absolute, fragment-free, and https or http://localhost; at least one redirect URI
/// and one scope are required.
/// </summary>
public record RegisterOrganizationClientRequest(
    string Kind,
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string? BackchannelLogoutUri = null);

/// <summary>
/// Everything about a client its organization may change after registration. Name and client id
/// are immutable and deliberately absent.
/// </summary>
public record UpdateOrganizationClientRequest(
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    string? BackchannelLogoutUri = null);
