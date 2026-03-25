using System.Diagnostics;
using System.Net.Http.Json;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Application.Telemetry;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Services;
using Wallow.Shared.Kernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OidcFederationService(
    ISsoConfigurationRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OidcFederationService> logger,
    TimeProvider timeProvider) : ISsoService
{
    private const string DiscoveryPath = ".well-known/openid-configuration";

    public async Task<SsoConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await repository.GetAsync(ct);
        return config is null ? null : MapToDto(config);
    }

    public async Task<SsoConfigurationDto> SaveOidcConfigurationAsync(
        SaveOidcConfigRequest request, CancellationToken ct = default)
    {
        using Activity? activity = IdentityModuleTelemetry.ActivitySource.StartActivity(
            "Identity.SaveOidcFederationConfig", ActivityKind.Internal);

        TenantId tenantId = tenantContext.TenantId;
        Guid userId = currentUserService.UserId ?? Guid.Empty;

        activity?.SetTag("identity.provider", "oidc");
        activity?.SetTag("identity.user_id", userId.ToString());

        LogSavingOidcConfig(tenantId.Value);

        SsoConfiguration? config = await repository.GetAsync(ct);
        if (config is null)
        {
            config = SsoConfiguration.Create(
                tenantId, request.DisplayName, SsoProtocol.Oidc,
                request.EmailAttribute, request.FirstNameAttribute,
                request.LastNameAttribute, userId, timeProvider);
            repository.Add(config);
        }

        config.UpdateOidcConfig(
            request.Issuer, request.ClientId, request.ClientSecret,
            request.Scopes, userId, timeProvider);

        config.UpdateBehaviorSettings(
            request.EnforceForAllUsers, request.AutoProvisionUsers, request.DefaultRole,
            request.SyncGroupsAsRoles, request.GroupsAttribute, userId, timeProvider);

        await repository.SaveChangesAsync(ct);

        IdentityModuleTelemetry.SsoLoginsTotal.Add(1,
            new KeyValuePair<string, object?>("provider", "oidc"));
        LogOidcConfigSaved(tenantId.Value);

        return MapToDto(config);
    }

    public async Task<SsoTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await repository.GetAsync(ct);
        if (config is null)
        {
            return new SsoTestResult(false, "SSO configuration not found");
        }

        if (string.IsNullOrWhiteSpace(config.OidcIssuer))
        {
            return new SsoTestResult(false, "OIDC Issuer not configured");
        }

        try
        {
            OidcDiscoveryDocument? discovery = await FetchDiscoveryDocumentAsync(config.OidcIssuer, ct);
            if (discovery is null)
            {
                return new SsoTestResult(false, "Failed to fetch OIDC discovery document");
            }

            if (discovery.Issuer != config.OidcIssuer)
            {
                return new SsoTestResult(false, "OIDC issuer mismatch in discovery document");
            }

            return new SsoTestResult(true, null);
        }
        catch (Exception ex)
        {
            IdentityModuleTelemetry.SsoFailuresTotal.Add(1,
                new KeyValuePair<string, object?>("provider", "oidc"));
            LogSsoConnectionTestFailed(ex, tenantContext.TenantId.Value);
            return new SsoTestResult(false, ex.Message);
        }
    }

    public async Task ActivateAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await repository.GetAsync(ct);
        if (config is null)
        {
            throw new InvalidOperationException("SSO configuration not found");
        }

        LogActivatingSso(tenantContext.TenantId.Value);

        Guid userId = currentUserService.UserId ?? Guid.Empty;
        config.Activate(userId, timeProvider);
        await repository.SaveChangesAsync(ct);

        IdentityModuleTelemetry.SsoLoginsTotal.Add(1,
            new KeyValuePair<string, object?>("provider", "oidc"));
        LogSsoActivated(tenantContext.TenantId.Value);
    }

    public async Task DisableAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await repository.GetAsync(ct);
        if (config is null)
        {
            throw new InvalidOperationException("SSO configuration not found");
        }

        LogDisablingSso(tenantContext.TenantId.Value);

        Guid userId = currentUserService.UserId ?? Guid.Empty;
        config.Disable(userId, timeProvider);
        await repository.SaveChangesAsync(ct);

        LogSsoDisabled(tenantContext.TenantId.Value);
    }

    public Task<OidcCallbackInfo> GetOidcCallbackInfoAsync(CancellationToken ct = default)
    {
        TenantId tenantId = tenantContext.TenantId;
        string schemeName = GetSchemeName(tenantId);
        string baseUrl = ResolveBaseUrl();

        string redirectUri = $"{baseUrl}/signin-oidc-{schemeName}";
        string postLogoutRedirectUri = $"{baseUrl}/signout-callback-oidc-{schemeName}";

        return Task.FromResult(new OidcCallbackInfo(
            redirectUri,
            postLogoutRedirectUri,
            $"wallow-{tenantId.Value.ToString()[..8]}"));
    }

    public async Task<SsoValidationResult> ValidateIdpConfigurationAsync(CancellationToken ct = default)
    {
        SsoConfiguration? config = await repository.GetAsync(ct);
        if (config is null)
        {
            return new SsoValidationResult(false, "SSO configuration not found", null, null, null);
        }

        if (string.IsNullOrWhiteSpace(config.OidcIssuer))
        {
            return new SsoValidationResult(false, "OIDC Issuer not configured", null, null, null);
        }

        if (string.IsNullOrWhiteSpace(config.OidcClientId))
        {
            return new SsoValidationResult(false, "OIDC Client ID not configured", null, null, null);
        }

        try
        {
            OidcDiscoveryDocument? discovery = await FetchDiscoveryDocumentAsync(config.OidcIssuer, ct);
            if (discovery is null)
            {
                return new SsoValidationResult(false,
                    "Failed to fetch OIDC discovery document", null, null, null);
            }

            return new SsoValidationResult(
                true, null,
                discovery.Issuer,
                discovery.AuthorizationEndpoint,
                null);
        }
        catch (Exception ex)
        {
            return new SsoValidationResult(false, ex.Message, null, null, null);
        }
    }

    private async Task<OidcDiscoveryDocument?> FetchDiscoveryDocumentAsync(string issuer, CancellationToken ct)
    {
        string discoveryUrl = $"{issuer.TrimEnd('/')}/{DiscoveryPath}";
        HttpClient httpClient = httpClientFactory.CreateClient("OidcDiscovery");
        HttpResponseMessage response = await httpClient.GetAsync(discoveryUrl, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<OidcDiscoveryDocument>(ct);
    }

    private static string GetSchemeName(TenantId tenantId) =>
        $"oidc-sso-{tenantId.Value.ToString()[..8]}";

    private string ResolveBaseUrl()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            HttpRequest request = httpContext.Request;
            return $"{request.Scheme}://{request.Host}";
        }

        return "https://localhost:5001";
    }

    private SsoConfigurationDto MapToDto(SsoConfiguration config)
    {
        string baseUrl = ResolveBaseUrl();
        string schemeName = GetSchemeName(tenantContext.TenantId);

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
            $"{baseUrl}/identity/sso",
            $"{baseUrl}/signin-oidc-{schemeName}",
            $"{baseUrl}/identity/sso/oidc/callback-info");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Saving OIDC federation config for tenant {TenantId}")]
    private partial void LogSavingOidcConfig(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC federation config saved for tenant {TenantId}")]
    private partial void LogOidcConfigSaved(Guid tenantId);

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

internal sealed record OidcDiscoveryDocument
{
    public string? Issuer { get; init; }
    public string? AuthorizationEndpoint { get; init; }
    public string? TokenEndpoint { get; init; }
    public string? UserinfoEndpoint { get; init; }
    public string? JwksUri { get; init; }
}
