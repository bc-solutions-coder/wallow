using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Events;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Identity.Domain.Entities;

/// <summary>
/// SSO identity provider configuration for a tenant, supporting SAML and OIDC protocols.
/// </summary>
/// <remarks>
/// State machine (SsoStatus):
/// <code>
///                    ┌──────────────────────┐
///                    │                      │
///   Create() ──► [Draft] ──► [Testing] ──► [Active]
///                  │  │                      │
///                  │  └──────────────────────┘
///                  │             │
///                  ▼             ▼
///              [Disabled] ◄── [Active]
///                  │
///                  └──────────► [Active]
///
///   Create()         → Draft
///   MoveToTesting()  → Draft → Testing
///   Activate()       → Draft, Testing, Disabled → Active
///   Disable()        → Draft, Testing, Active → Disabled
///
///   Config updates (SAML/OIDC/Behavior) are blocked when Active.
/// </code>
/// </remarks>
public sealed class SsoConfiguration : AggregateRoot<SsoConfigurationId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public string DisplayName { get; private set; } = string.Empty;
    public SsoProtocol Protocol { get; private set; }
    public SsoStatus Status { get; private set; }

    // SAML Configuration
    public string? SamlEntityId { get; private set; }
    public string? SamlSsoUrl { get; private set; }
    public string? SamlSloUrl { get; private set; }
    public string? SamlCertificate { get; private set; }
    public SamlNameIdFormat? SamlNameIdFormat { get; private set; }

    // OIDC Configuration
    public string? OidcIssuer { get; private set; }
    public string? OidcClientId { get; private set; }
    public string? OidcClientSecret { get; private set; }
    public string? OidcScopes { get; private set; }

    // Attribute Mapping
    public string EmailAttribute { get; private set; } = string.Empty;
    public string FirstNameAttribute { get; private set; } = string.Empty;
    public string LastNameAttribute { get; private set; } = string.Empty;
    public string? GroupsAttribute { get; private set; }

    // Behavior Settings
    public bool EnforceForAllUsers { get; private set; }
    public bool AutoProvisionUsers { get; private set; }
    public string? DefaultRole { get; private set; }
    public bool SyncGroupsAsRoles { get; private set; }

    // Keycloak Reference
    public string? KeycloakIdpAlias { get; private set; }

    private SsoConfiguration() { }

    private SsoConfiguration(
        TenantId tenantId,
        string displayName,
        SsoProtocol protocol,
        string emailAttribute,
        string firstNameAttribute,
        string lastNameAttribute,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        Id = SsoConfigurationId.New();
        TenantId = tenantId;
        DisplayName = displayName;
        Protocol = protocol;
        Status = SsoStatus.Draft;
        EmailAttribute = emailAttribute;
        FirstNameAttribute = firstNameAttribute;
        LastNameAttribute = lastNameAttribute;
        EnforceForAllUsers = false;
        AutoProvisionUsers = true;
        SyncGroupsAsRoles = false;
        SetCreated(timeProvider.GetUtcNow(), createdByUserId);
    }

    public static SsoConfiguration Create(
        TenantId tenantId,
        string displayName,
        SsoProtocol protocol,
        string emailAttribute,
        string firstNameAttribute,
        string lastNameAttribute,
        Guid createdByUserId,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new BusinessRuleException(
                "Identity.DisplayNameRequired",
                "SSO configuration display name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(emailAttribute))
        {
            throw new BusinessRuleException(
                "Identity.EmailAttributeRequired",
                "Email attribute mapping is required");
        }

        if (string.IsNullOrWhiteSpace(firstNameAttribute))
        {
            throw new BusinessRuleException(
                "Identity.FirstNameAttributeRequired",
                "First name attribute mapping is required");
        }

        if (string.IsNullOrWhiteSpace(lastNameAttribute))
        {
            throw new BusinessRuleException(
                "Identity.LastNameAttributeRequired",
                "Last name attribute mapping is required");
        }

        return new SsoConfiguration(
            tenantId,
            displayName,
            protocol,
            emailAttribute,
            firstNameAttribute,
            lastNameAttribute,
            createdByUserId,
            timeProvider);
    }

    public void Activate(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status == SsoStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.SsoAlreadyActive",
                "SSO configuration is already active");
        }

        if (Protocol == SsoProtocol.Saml && string.IsNullOrWhiteSpace(SamlEntityId))
        {
            throw new BusinessRuleException(
                "Identity.SamlConfigurationIncomplete",
                "SAML configuration is incomplete");
        }

        if (Protocol == SsoProtocol.Oidc && string.IsNullOrWhiteSpace(OidcIssuer))
        {
            throw new BusinessRuleException(
                "Identity.OidcConfigurationIncomplete",
                "OIDC configuration is incomplete");
        }

        Status = SsoStatus.Active;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);

        RaiseDomainEvent(new SsoConfigurationActivatedEvent(
            Id.Value,
            TenantId.Value,
            DisplayName,
            Protocol.ToString().ToUpperInvariant()));
    }

    public void Disable(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status == SsoStatus.Disabled)
        {
            throw new BusinessRuleException(
                "Identity.SsoAlreadyDisabled",
                "SSO configuration is already disabled");
        }

        Status = SsoStatus.Disabled;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void UpdateSamlConfig(
        string entityId,
        string ssoUrl,
        string? sloUrl,
        string certificate,
        SamlNameIdFormat nameIdFormat,
        Guid updatedByUserId,
        TimeProvider timeProvider)
    {
        if (Protocol != SsoProtocol.Saml)
        {
            throw new BusinessRuleException(
                "Identity.NotSamlConfiguration",
                "Cannot update SAML configuration for non-SAML protocol");
        }

        if (Status == SsoStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.CannotUpdateActiveConfiguration",
                "Cannot update active SSO configuration");
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new BusinessRuleException(
                "Identity.SamlEntityIdRequired",
                "SAML entity ID is required");
        }

        if (string.IsNullOrWhiteSpace(ssoUrl))
        {
            throw new BusinessRuleException(
                "Identity.SamlSsoUrlRequired",
                "SAML SSO URL is required");
        }

        if (string.IsNullOrWhiteSpace(certificate))
        {
            throw new BusinessRuleException(
                "Identity.SamlCertificateRequired",
                "SAML certificate is required");
        }

        SamlEntityId = entityId;
        SamlSsoUrl = ssoUrl;
        SamlSloUrl = sloUrl;
        SamlCertificate = certificate;
        SamlNameIdFormat = nameIdFormat;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void UpdateOidcConfig(
        string issuer,
        string clientId,
        string clientSecret,
        string scopes,
        Guid updatedByUserId,
        TimeProvider timeProvider)
    {
        if (Protocol != SsoProtocol.Oidc)
        {
            throw new BusinessRuleException(
                "Identity.NotOidcConfiguration",
                "Cannot update OIDC configuration for non-OIDC protocol");
        }

        if (Status == SsoStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.CannotUpdateActiveConfiguration",
                "Cannot update active SSO configuration");
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new BusinessRuleException(
                "Identity.OidcIssuerRequired",
                "OIDC issuer is required");
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new BusinessRuleException(
                "Identity.OidcClientIdRequired",
                "OIDC client ID is required");
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new BusinessRuleException(
                "Identity.OidcClientSecretRequired",
                "OIDC client secret is required");
        }

        OidcIssuer = issuer;
        OidcClientId = clientId;
        OidcClientSecret = clientSecret;
        OidcScopes = scopes;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void UpdateBehaviorSettings(
        bool enforceForAllUsers,
        bool autoProvisionUsers,
        string? defaultRole,
        bool syncGroupsAsRoles,
        string? groupsAttribute,
        Guid updatedByUserId,
        TimeProvider timeProvider)
    {
        if (Status == SsoStatus.Active)
        {
            throw new BusinessRuleException(
                "Identity.CannotUpdateActiveConfiguration",
                "Cannot update active SSO configuration");
        }

        EnforceForAllUsers = enforceForAllUsers;
        AutoProvisionUsers = autoProvisionUsers;
        DefaultRole = defaultRole;
        SyncGroupsAsRoles = syncGroupsAsRoles;
        GroupsAttribute = groupsAttribute;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void SetKeycloakIdpAlias(string alias, Guid updatedByUserId, TimeProvider timeProvider)
    {
        KeycloakIdpAlias = alias;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }

    public void MoveToTesting(Guid updatedByUserId, TimeProvider timeProvider)
    {
        if (Status != SsoStatus.Draft)
        {
            throw new BusinessRuleException(
                "Identity.InvalidStatusTransition",
                "Can only move to Testing from Draft status");
        }

        Status = SsoStatus.Testing;
        SetUpdated(timeProvider.GetUtcNow(), updatedByUserId);
    }
}
