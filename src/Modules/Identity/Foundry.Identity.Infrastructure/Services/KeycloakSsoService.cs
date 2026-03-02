using System.Diagnostics;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Application.Telemetry;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakSsoService : ISsoService
{
    private readonly HttpClient _httpClient;
    private readonly ISsoConfigurationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly KeycloakAuthenticationOptions _keycloakOptions;
    private readonly ILogger<KeycloakSsoService> _logger;
    private readonly SsoClaimsSyncService _claimsSyncService;
    private readonly KeycloakIdpService _idpService;
    private const string Realm = "foundry";

    public KeycloakSsoService(
        IHttpClientFactory httpClientFactory,
        ISsoConfigurationRepository repository,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        IOptions<KeycloakAuthenticationOptions> keycloakOptions,
        ILogger<KeycloakSsoService> logger,
        SsoClaimsSyncService claimsSyncService,
        KeycloakIdpService idpService)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _keycloakOptions = keycloakOptions.Value;
        _logger = logger;
        _claimsSyncService = claimsSyncService;
        _idpService = idpService;
    }

    public async Task<SsoConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            return null;
        }

        return MapToDto(config);
    }

    public Task<SsoConfigurationDto> SaveSamlConfigurationAsync(SaveSamlConfigRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string alias = $"saml-{tenantId.Value.ToString()[..8]}";

        Dictionary<string, object> idpConfig = new Dictionary<string, object>
        {
            ["alias"] = alias,
            ["displayName"] = request.DisplayName,
            ["providerId"] = "saml",
            ["enabled"] = false,
            ["trustEmail"] = true,
            ["firstBrokerLoginFlowAlias"] = "first broker login",
            ["config"] = new Dictionary<string, string>
            {
                ["entityId"] = request.EntityId,
                ["singleSignOnServiceUrl"] = request.SsoUrl,
                ["singleLogoutServiceUrl"] = request.SloUrl ?? "",
                ["signingCertificate"] = NormalizeCertificate(request.Certificate),
                ["nameIDPolicyFormat"] = ToKeycloakNameIdFormat(request.NameIdFormat),
                ["principalType"] = "ATTRIBUTE",
                ["principalAttribute"] = request.EmailAttribute,
                ["wantAuthnRequestsSigned"] = "true",
                ["wantAssertionsSigned"] = "true",
                ["postBindingResponse"] = "true",
                ["postBindingAuthnRequest"] = "true"
            }
        };

        return SaveIdpConfigurationCoreAsync(
            alias, SsoProtocol.SAML, request.DisplayName,
            request.EmailAttribute, request.FirstNameAttribute, request.LastNameAttribute,
            request.EnforceForAllUsers, request.AutoProvisionUsers, request.DefaultRole,
            request.SyncGroupsAsRoles, request.GroupsAttribute,
            (config, userId) => config.UpdateSamlConfig(
                request.EntityId, request.SsoUrl, request.SloUrl,
                request.Certificate, request.NameIdFormat, userId),
            idpConfig, ct);
    }

    public Task<SsoConfigurationDto> SaveOidcConfigurationAsync(SaveOidcConfigRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string alias = $"oidc-{tenantId.Value.ToString()[..8]}";

        Dictionary<string, object> idpConfig = new Dictionary<string, object>
        {
            ["alias"] = alias,
            ["displayName"] = request.DisplayName,
            ["providerId"] = "oidc",
            ["enabled"] = false,
            ["trustEmail"] = true,
            ["firstBrokerLoginFlowAlias"] = "first broker login",
            ["config"] = new Dictionary<string, string>
            {
                ["issuer"] = request.Issuer,
                ["authorizationUrl"] = $"{request.Issuer}/protocol/openid-connect/auth",
                ["tokenUrl"] = $"{request.Issuer}/protocol/openid-connect/token",
                ["userInfoUrl"] = $"{request.Issuer}/protocol/openid-connect/userinfo",
                ["clientId"] = request.ClientId,
                ["clientSecret"] = request.ClientSecret,
                ["defaultScope"] = request.Scopes,
                ["validateSignature"] = "true",
                ["useJwksUrl"] = "true",
                ["jwksUrl"] = $"{request.Issuer}/protocol/openid-connect/certs"
            }
        };

        return SaveIdpConfigurationCoreAsync(
            alias, SsoProtocol.OIDC, request.DisplayName,
            request.EmailAttribute, request.FirstNameAttribute, request.LastNameAttribute,
            request.EnforceForAllUsers, request.AutoProvisionUsers, request.DefaultRole,
            request.SyncGroupsAsRoles, request.GroupsAttribute,
            (config, userId) => config.UpdateOidcConfig(
                request.Issuer, request.ClientId, request.ClientSecret,
                request.Scopes, userId),
            idpConfig, ct);
    }

    private async Task<SsoConfigurationDto> SaveIdpConfigurationCoreAsync(
        string alias,
        SsoProtocol protocol,
        string displayName,
        string emailAttribute,
        string firstNameAttribute,
        string lastNameAttribute,
        bool enforceForAllUsers,
        bool autoProvisionUsers,
        string? defaultRole,
        bool syncGroupsAsRoles,
        string? groupsAttribute,
        Action<SsoConfiguration, Guid> applyProtocolConfig,
        Dictionary<string, object> idpConfig,
        CancellationToken ct)
    {
        using Activity? activity = IdentityModuleTelemetry.ActivitySource.StartActivity("Identity.SaveIdpConfiguration");

        TenantId tenantId = _tenantContext.TenantId;
        Guid userId = _currentUserService.UserId ?? Guid.Empty;
        string protocolName = protocol.ToString();

        activity?.SetTag("identity.provider", protocolName.ToLowerInvariant());
        activity?.SetTag("identity.user_id", userId.ToString());

        LogSavingIdpConfig(protocolName, tenantId.Value);

        // Get or create local configuration
        SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            config = SsoConfiguration.Create(
                tenantId, displayName, protocol,
                emailAttribute, firstNameAttribute, lastNameAttribute, userId);
            _repository.Add(config);
        }

        // Apply protocol-specific configuration
        applyProtocolConfig(config, userId);

        // Update behavior settings
        config.UpdateBehaviorSettings(
            enforceForAllUsers, autoProvisionUsers, defaultRole,
            syncGroupsAsRoles, groupsAttribute, userId);

        // Create or update Keycloak Identity Provider
        bool idpExists = await _idpService.IdentityProviderExistsAsync(alias, ct);
        if (idpExists)
        {
            HttpResponseMessage updateResponse = await _httpClient.PutAsJsonAsync(
                $"/admin/realms/{Realm}/identity-provider/instances/{alias}",
                idpConfig, ct);
            updateResponse.EnsureSuccessStatusCode();
            LogUpdatedIdp(protocolName, alias);
        }
        else
        {
            HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(
                $"/admin/realms/{Realm}/identity-provider/instances",
                idpConfig, ct);
            createResponse.EnsureSuccessStatusCode();
            LogCreatedIdp(protocolName, alias);
        }

        // Create attribute mappers
        await _idpService.CreateAttributeMappersAsync(alias, emailAttribute, firstNameAttribute, lastNameAttribute, ct);

        // Store the alias reference
        config.SetKeycloakIdpAlias(alias, userId);
        await _repository.SaveChangesAsync(ct);

        IdentityModuleTelemetry.SsoLoginsTotal.Add(1, new KeyValuePair<string, object?>("provider", protocolName));
        LogIdpConfigSaved(protocolName, tenantId.Value);
        return MapToDto(config);
    }

    public async Task<SsoTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            return new SsoTestResult(false, "SSO configuration not found");
        }

        try
        {
            if (config.Protocol == SsoProtocol.SAML)
            {
                return await _idpService.TestSamlConnectionAsync(config, ct);
            }
            else
            {
                return await _idpService.TestOidcConnectionAsync(config, ct);
            }
        }
        catch (Exception ex)
        {
            IdentityModuleTelemetry.SsoFailuresTotal.Add(1, new KeyValuePair<string, object?>("provider", config.Protocol.ToString()));
            LogSsoConnectionTestFailed(ex, _tenantContext.TenantId.Value);
            return new SsoTestResult(false, ex.Message);
        }
    }

    public async Task ActivateAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            throw new InvalidOperationException("SSO configuration not found");
        }

        if (string.IsNullOrWhiteSpace(config.KeycloakIdpAlias))
        {
            throw new InvalidOperationException("Keycloak IdP not configured");
        }

        LogActivatingSso(_tenantContext.TenantId.Value);

        // Enable IdP in Keycloak
        await _idpService.EnableIdentityProviderAsync(config.KeycloakIdpAlias, true, ct);

        // Update local status
        config.Activate(_currentUserService.UserId ?? Guid.Empty);
        await _repository.SaveChangesAsync(ct);

        IdentityModuleTelemetry.SsoLoginsTotal.Add(1, new KeyValuePair<string, object?>("provider", config.Protocol.ToString()));
        LogSsoActivated(_tenantContext.TenantId.Value);
    }

    public async Task DisableAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            throw new InvalidOperationException("SSO configuration not found");
        }

        LogDisablingSso(_tenantContext.TenantId.Value);

        // Disable IdP in Keycloak if configured
        if (!string.IsNullOrWhiteSpace(config.KeycloakIdpAlias))
        {
            await _idpService.EnableIdentityProviderAsync(config.KeycloakIdpAlias, false, ct);
        }

        // Update local status
        config.Disable(_currentUserService.UserId ?? Guid.Empty);
        await _repository.SaveChangesAsync(ct);

        LogSsoDisabled(_tenantContext.TenantId.Value);
    }

    public Task<string> GetSamlServiceProviderMetadataAsync(CancellationToken ct = default)
    {
        string entityId = GetServiceProviderEntityId();
        string acsUrl = GetServiceProviderAcsUrl();
        string sloUrl = GetServiceProviderSloUrl();

        // Generate SAML SP metadata XML
        string metadata = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<md:EntityDescriptor xmlns:md=""urn:oasis:names:tc:SAML:2.0:metadata"" entityID=""{entityId}"">
  <md:SPSSODescriptor AuthnRequestsSigned=""true"" WantAssertionsSigned=""true"" protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol"">
    <md:NameIDFormat>urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress</md:NameIDFormat>
    <md:NameIDFormat>urn:oasis:names:tc:SAML:2.0:nameid-format:persistent</md:NameIDFormat>
    <md:AssertionConsumerService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""{acsUrl}"" index=""1"" isDefault=""true""/>
    <md:SingleLogoutService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""{sloUrl}""/>
  </md:SPSSODescriptor>
</md:EntityDescriptor>";

        return Task.FromResult(metadata);
    }

    public Task<OidcCallbackInfo> GetOidcCallbackInfoAsync(CancellationToken ct = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string alias = $"oidc-{tenantId.Value.ToString()[..8]}";

        string redirectUri = $"{_keycloakOptions.AuthServerUrl}/realms/{Realm}/broker/{alias}/endpoint";
        string postLogoutRedirectUri = $"{_keycloakOptions.AuthServerUrl}/realms/{Realm}/broker/{alias}/endpoint/logout_response";

        return Task.FromResult(new OidcCallbackInfo(
            redirectUri,
            postLogoutRedirectUri,
            $"foundry-{Realm}"));
    }

    public async Task<SsoValidationResult> ValidateIdpConfigurationAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await _repository.GetAsync(ct);
        if (config == null)
        {
            return new SsoValidationResult(false, "SSO configuration not found", null, null, null);
        }

        try
        {
            if (config.Protocol == SsoProtocol.SAML)
            {
                return KeycloakIdpService.ValidateSamlConfiguration(config);
            }
            else
            {
                return await _idpService.ValidateOidcConfigurationAsync(config, ct);
            }
        }
        catch (Exception ex)
        {
            return new SsoValidationResult(false, ex.Message, null, null, null);
        }
    }

    public async Task SyncUserClaimsAsync(Guid userId, CancellationToken ct = default)
    {
        using Activity? activity = IdentityModuleTelemetry.ActivitySource.StartActivity("Identity.SyncUser");
        activity?.SetTag("identity.user_id", userId.ToString());
        activity?.SetTag("identity.provider", "keycloak");

        try
        {
            await _claimsSyncService.SyncUserClaimsAsync(userId, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message }
            }));
            throw;
        }
    }

    private SsoConfigurationDto MapToDto(SsoConfiguration config)
    {
        return new SsoConfigurationDto(
            config.Id,
            config.DisplayName,
            config.Protocol,
            config.Status,
            config.SamlEntityId,
            config.SamlSsoUrl,
            !string.IsNullOrWhiteSpace(config.SamlEntityId),
            config.OidcIssuer,
            config.OidcClientId,
            !string.IsNullOrWhiteSpace(config.OidcIssuer),
            config.EnforceForAllUsers,
            config.AutoProvisionUsers,
            config.DefaultRole,
            config.SyncGroupsAsRoles,
            GetServiceProviderEntityId(),
            GetServiceProviderAcsUrl(),
            GetServiceProviderMetadataUrl());
    }

    private string GetServiceProviderEntityId()
    {
        return $"{_keycloakOptions.AuthServerUrl}/realms/{Realm}";
    }

    private string GetServiceProviderAcsUrl()
    {
        TenantId tenantId = _tenantContext.TenantId;
        string alias = $"saml-{tenantId.Value.ToString()[..8]}";
        return $"{_keycloakOptions.AuthServerUrl}/realms/{Realm}/broker/{alias}/endpoint";
    }

    private string GetServiceProviderSloUrl()
    {
        TenantId tenantId = _tenantContext.TenantId;
        string alias = $"saml-{tenantId.Value.ToString()[..8]}";
        return $"{_keycloakOptions.AuthServerUrl}/realms/{Realm}/broker/{alias}/endpoint/logout_response";
    }

    private string GetServiceProviderMetadataUrl()
    {
        TenantId tenantId = _tenantContext.TenantId;
        string alias = $"saml-{tenantId.Value.ToString()[..8]}";
        return $"{_keycloakOptions.AuthServerUrl}/realms/{Realm}/broker/{alias}/endpoint/descriptor";
    }

    private static string NormalizeCertificate(string certificate)
    {
        // Remove PEM headers/footers and whitespace
        return certificate
            .Replace("-----BEGIN CERTIFICATE-----", "", StringComparison.Ordinal)
            .Replace("-----END CERTIFICATE-----", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Trim();
    }

    private static string ToKeycloakNameIdFormat(SamlNameIdFormat format)
    {
        return format switch
        {
            SamlNameIdFormat.Email => "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
            SamlNameIdFormat.Persistent => "urn:oasis:names:tc:SAML:2.0:nameid-format:persistent",
            SamlNameIdFormat.Transient => "urn:oasis:names:tc:SAML:2.0:nameid-format:transient",
            SamlNameIdFormat.Unspecified => "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified",
            _ => "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"
        };
    }
}

internal sealed record KeycloakUserRepresentation
{
    public string? Id { get; init; }
    public string? Email { get; init; }
    public Dictionary<string, IEnumerable<string>>? Attributes { get; init; }
}

internal sealed record KeycloakRoleRepresentation
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public sealed partial class KeycloakSsoService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Saving {Protocol} SSO configuration for tenant {TenantId}")]
    private partial void LogSavingIdpConfig(string protocol, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated {Protocol} IdP {Alias} in Keycloak")]
    private partial void LogUpdatedIdp(string protocol, string alias);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {Protocol} IdP {Alias} in Keycloak")]
    private partial void LogCreatedIdp(string protocol, string alias);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Protocol} SSO configuration saved for tenant {TenantId}")]
    private partial void LogIdpConfigSaved(string protocol, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SSO connection test failed for tenant {TenantId}")]
    private partial void LogSsoConnectionTestFailed(Exception ex, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Activating SSO for tenant {TenantId}")]
    private partial void LogActivatingSso(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SSO activated for tenant {TenantId}")]
    private partial void LogSsoActivated(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disabling SSO for tenant {TenantId}")]
    private partial void LogDisablingSso(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SSO disabled for tenant {TenantId}")]
    private partial void LogSsoDisabled(Guid tenantId);
}
