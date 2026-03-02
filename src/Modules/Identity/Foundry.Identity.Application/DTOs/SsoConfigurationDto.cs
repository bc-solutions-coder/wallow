using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;

namespace Foundry.Identity.Application.DTOs;

/// <summary>
/// SSO configuration details for a tenant.
/// </summary>
public record SsoConfigurationDto(
    SsoConfigurationId? Id,
    string? DisplayName,
    SsoProtocol? Protocol,
    SsoStatus Status,

    // SAML Configuration
    string? SamlEntityId,
    string? SamlSsoUrl,
    bool SamlConfigured,

    // OIDC Configuration
    string? OidcIssuer,
    string? OidcClientId,
    bool OidcConfigured,

    // Behavior Settings
    bool EnforceForAllUsers,
    bool AutoProvisionUsers,
    string? DefaultRole,
    bool SyncGroupsAsRoles,

    // For tenant's IdP setup (SP metadata endpoints)
    string ServiceProviderEntityId,
    string ServiceProviderAcsUrl,
    string ServiceProviderMetadataUrl);

/// <summary>
/// Request to save SAML SSO configuration.
/// </summary>
public record SaveSamlConfigRequest(
    string DisplayName,
    string EntityId,
    string SsoUrl,
    string? SloUrl,
    string Certificate,
    SamlNameIdFormat NameIdFormat,
    string EmailAttribute,
    string FirstNameAttribute,
    string LastNameAttribute,
    string? GroupsAttribute,
    bool EnforceForAllUsers,
    bool AutoProvisionUsers,
    string? DefaultRole,
    bool SyncGroupsAsRoles);

/// <summary>
/// Request to save OIDC SSO configuration.
/// </summary>
public record SaveOidcConfigRequest(
    string DisplayName,
    string Issuer,
    string ClientId,
    string ClientSecret,
    string Scopes,
    string EmailAttribute,
    string FirstNameAttribute,
    string LastNameAttribute,
    string? GroupsAttribute,
    bool EnforceForAllUsers,
    bool AutoProvisionUsers,
    string? DefaultRole,
    bool SyncGroupsAsRoles);

/// <summary>
/// Result of testing SSO connection.
/// </summary>
public record SsoTestResult(
    bool Success,
    string? ErrorMessage);

/// <summary>
/// OIDC callback information for IdP configuration.
/// </summary>
public record OidcCallbackInfo(
    string RedirectUri,
    string PostLogoutRedirectUri,
    string ClientId);

/// <summary>
/// Result of validating IdP configuration.
/// </summary>
public record SsoValidationResult(
    bool IsValid,
    string? ErrorMessage,
    string? IdpEntityId,
    string? IdpSsoUrl,
    DateTime? CertificateExpiry);
