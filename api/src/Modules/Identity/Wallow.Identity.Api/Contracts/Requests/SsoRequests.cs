using Wallow.Identity.Api.Contracts.Enums;

namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Request to configure SAML SSO.
/// </summary>
public record ConfigureSamlSsoRequest(
    string DisplayName,
    string EntityId,
    string SsoUrl,
    string? SloUrl,
    string Certificate,
    ApiSamlNameIdFormat NameIdFormat,
    string EmailAttribute = "email",
    string FirstNameAttribute = "firstName",
    string LastNameAttribute = "lastName",
    string? GroupsAttribute = null,
    bool EnforceForAllUsers = false,
    bool AutoProvisionUsers = true,
    string? DefaultRole = null,
    bool SyncGroupsAsRoles = false);

/// <summary>
/// Request to configure OIDC SSO.
/// </summary>
public record ConfigureOidcSsoRequest(
    string DisplayName,
    string Issuer,
    string ClientId,
    string ClientSecret,
    string Scopes = "openid profile email",
    string EmailAttribute = "email",
    string FirstNameAttribute = "given_name",
    string LastNameAttribute = "family_name",
    string? GroupsAttribute = null,
    bool EnforceForAllUsers = false,
    bool AutoProvisionUsers = true,
    string? DefaultRole = null,
    bool SyncGroupsAsRoles = false);
