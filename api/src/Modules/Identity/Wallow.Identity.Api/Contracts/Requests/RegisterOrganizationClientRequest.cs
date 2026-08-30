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
