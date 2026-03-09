using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Domain.Entities;
using Microsoft.Extensions.Logging;
using Foundry.Identity.Infrastructure.Extensions;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakIdpService
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _externalHttpClient;
    private readonly ILogger<KeycloakIdpService> _logger;
    private readonly string _realm;

    public KeycloakIdpService(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakOptions> keycloakOptions,
        ILogger<KeycloakIdpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _externalHttpClient = httpClientFactory.CreateClient();
        _realm = keycloakOptions.Value.Realm;
        _logger = logger;
    }

    public async Task<bool> IdentityProviderExistsAsync(string alias, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"/admin/realms/{_realm}/identity-provider/instances/{alias}",
                ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnableIdentityProviderAsync(string alias, bool enabled, CancellationToken ct)
    {
        // Get current IdP config
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/admin/realms/{_realm}/identity-provider/instances/{alias}",
            ct);
        await response.EnsureSuccessOrThrowAsync(ct);

        Dictionary<string, object>? idpConfig = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct);
        if (idpConfig == null)
        {
            throw new InvalidOperationException($"Identity provider {alias} not found");
        }

        // Update enabled flag
        idpConfig["enabled"] = enabled;

        HttpResponseMessage updateResponse = await _httpClient.PutAsJsonAsync(
            $"/admin/realms/{_realm}/identity-provider/instances/{alias}",
            idpConfig,
            ct);
        await updateResponse.EnsureSuccessOrThrowAsync(ct);
    }

    public async Task CreateAttributeMappersAsync(
        string alias,
        string emailAttribute,
        string firstNameAttribute,
        string lastNameAttribute,
        CancellationToken ct)
    {
        Dictionary<string, object>[] mappers =
        [
            CreateAttributeMapper(alias, "email", emailAttribute, "email"),
            CreateAttributeMapper(alias, "firstName", firstNameAttribute, "firstName"),
            CreateAttributeMapper(alias, "lastName", lastNameAttribute, "lastName")
        ];

        foreach (Dictionary<string, object> mapper in mappers)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    $"/admin/realms/{_realm}/identity-provider/instances/{alias}/mappers",
                    mapper,
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    // Try to update if mapper already exists
                    List<IdpMapperRepresentation>? existingMappers = await _httpClient.GetFromJsonAsync<List<IdpMapperRepresentation>>(
                        $"/admin/realms/{_realm}/identity-provider/instances/{alias}/mappers",
                        ct);

                    IdpMapperRepresentation? existing = existingMappers?.FirstOrDefault(m => m.Name == Convert.ToString(mapper["name"], CultureInfo.InvariantCulture));
                    if (existing?.Id != null)
                    {
                        mapper["id"] = existing.Id;
                        await _httpClient.PutAsJsonAsync(
                            $"/admin/realms/{_realm}/identity-provider/instances/{alias}/mappers/{existing.Id}",
                            mapper,
                            ct);
                    }
                }
            }
            catch (Exception ex)
            {
                LogCreateMapperFailed(ex, Convert.ToString(mapper["name"], CultureInfo.InvariantCulture) ?? string.Empty, alias);
            }
        }
    }

    public async Task<SsoTestResult> TestSamlConnectionAsync(SsoConfiguration config, CancellationToken ct)
    {
        // Validate SAML SSO URL is reachable
        if (string.IsNullOrWhiteSpace(config.SamlSsoUrl))
        {
            return new SsoTestResult(false, "SAML SSO URL not configured");
        }

        HttpResponseMessage response = await _externalHttpClient.GetAsync(config.SamlSsoUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            return new SsoTestResult(false,
                $"SAML SSO URL returned {response.StatusCode}");
        }

        // Validate certificate
        if (!string.IsNullOrWhiteSpace(config.SamlCertificate))
        {
            try
            {
                byte[] certBytes = Convert.FromBase64String(NormalizeCertificate(config.SamlCertificate));
                X509Certificate2 cert = X509CertificateLoader.LoadCertificate(certBytes);
                if (cert.NotAfter < DateTime.UtcNow)
                {
                    return new SsoTestResult(false,
                        $"Certificate expired on {cert.NotAfter:yyyy-MM-dd}");
                }
            }
            catch (Exception ex)
            {
                return new SsoTestResult(false,
                    $"Invalid certificate: {ex.Message}");
            }
        }

        return new SsoTestResult(true, null);
    }

    public async Task<SsoTestResult> TestOidcConnectionAsync(SsoConfiguration config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.OidcIssuer))
        {
            return new SsoTestResult(false, "OIDC Issuer not configured");
        }

        // Test OIDC discovery endpoint
        string discoveryUrl = $"{config.OidcIssuer.TrimEnd('/')}/.well-known/openid-configuration";
        try
        {
            HttpResponseMessage response = await _externalHttpClient.GetAsync(discoveryUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new SsoTestResult(false,
                    $"OIDC discovery endpoint returned {response.StatusCode}");
            }

            OidcDiscoveryDocument? discovery = await response.Content.ReadFromJsonAsync<OidcDiscoveryDocument>(ct);
            if (discovery?.Issuer != config.OidcIssuer)
            {
                return new SsoTestResult(false,
                    "OIDC issuer mismatch in discovery document");
            }

            return new SsoTestResult(true, null);
        }
        catch (Exception ex)
        {
            return new SsoTestResult(false,
                $"Failed to fetch OIDC discovery: {ex.Message}");
        }
    }

    public static SsoValidationResult ValidateSamlConfiguration(SsoConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.SamlEntityId))
        {
            return new SsoValidationResult(false, "SAML Entity ID not configured", null, null, null);
        }

        if (string.IsNullOrWhiteSpace(config.SamlSsoUrl))
        {
            return new SsoValidationResult(false, "SAML SSO URL not configured", null, null, null);
        }

        DateTime? certExpiry = null;
        if (!string.IsNullOrWhiteSpace(config.SamlCertificate))
        {
            try
            {
                byte[] certBytes = Convert.FromBase64String(NormalizeCertificate(config.SamlCertificate));
                X509Certificate2 cert = X509CertificateLoader.LoadCertificate(certBytes);
                certExpiry = cert.NotAfter;
            }
            catch
            {
                return new SsoValidationResult(false, "Invalid certificate format", null, null, null);
            }
        }

        return new SsoValidationResult(
            true,
            null,
            config.SamlEntityId,
            config.SamlSsoUrl,
            certExpiry);
    }

    public async Task<SsoValidationResult> ValidateOidcConfigurationAsync(SsoConfiguration config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.OidcIssuer))
        {
            return new SsoValidationResult(false, "OIDC Issuer not configured", null, null, null);
        }

        if (string.IsNullOrWhiteSpace(config.OidcClientId))
        {
            return new SsoValidationResult(false, "OIDC Client ID not configured", null, null, null);
        }

        string discoveryUrl = $"{config.OidcIssuer.TrimEnd('/')}/.well-known/openid-configuration";
        try
        {
            HttpResponseMessage response = await _externalHttpClient.GetAsync(discoveryUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new SsoValidationResult(false,
                    "Failed to fetch OIDC discovery document",
                    null, null, null);
            }

            OidcDiscoveryDocument? discovery = await response.Content.ReadFromJsonAsync<OidcDiscoveryDocument>(ct);
            return new SsoValidationResult(
                true,
                null,
                discovery?.Issuer,
                discovery?.AuthorizationEndpoint,
                null);
        }
        catch (Exception ex)
        {
            return new SsoValidationResult(false, ex.Message, null, null, null);
        }
    }

    private static Dictionary<string, object> CreateAttributeMapper(
        string alias,
        string name,
        string attributeName,
        string userAttribute)
    {
        return new Dictionary<string, object>
        {
            ["name"] = $"{name}-mapper",
            ["identityProviderAlias"] = alias,
            ["identityProviderMapper"] = "hardcoded-attribute-idp-mapper",
            ["config"] = new Dictionary<string, string>
            {
                ["syncMode"] = "INHERIT",
                ["attribute"] = attributeName,
                ["attribute.value"] = userAttribute
            }
        };
    }

    private static string NormalizeCertificate(string certificate)
    {
        return certificate
            .Replace("-----BEGIN CERTIFICATE-----", "", StringComparison.Ordinal)
            .Replace("-----END CERTIFICATE-----", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Trim();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to create/update mapper {MapperName} for IdP {Alias}")]
    private partial void LogCreateMapperFailed(Exception ex, string mapperName, string alias);
}

file sealed record IdpMapperRepresentation(string? Id, string? Name);

file sealed record OidcDiscoveryDocument
{
    public string? Issuer { get; init; }
    public string? AuthorizationEndpoint { get; init; }
    public string? TokenEndpoint { get; init; }
    public string? UserinfoEndpoint { get; init; }
    public string? JwksUri { get; init; }
}
