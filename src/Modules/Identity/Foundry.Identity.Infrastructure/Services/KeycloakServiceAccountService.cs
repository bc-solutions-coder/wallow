using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class KeycloakServiceAccountService : IServiceAccountService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceAccountRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly KeycloakAuthenticationOptions _keycloakOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KeycloakServiceAccountService> _logger;
    private readonly string _realm;

    public KeycloakServiceAccountService(
        IHttpClientFactory httpClientFactory,
        IServiceAccountRepository repository,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        IOptions<KeycloakAuthenticationOptions> keycloakOptions,
        IOptions<KeycloakOptions> keycloakRealmOptions,
        TimeProvider timeProvider,
        ILogger<KeycloakServiceAccountService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("KeycloakAdminClient");
        _repository = repository;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _keycloakOptions = keycloakOptions.Value;
        _realm = keycloakRealmOptions.Value.Realm;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ServiceAccountCreatedResult> CreateAsync(CreateServiceAccountRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = _tenantContext.TenantId;
        string clientId = $"sa-{tenantId.Value.ToString()[..8]}-{Slugify(request.Name)}";

        LogCreatingServiceAccount(clientId, tenantId.Value);

        // Create Keycloak client with client_credentials grant
        var clientRepresentation = new
        {
            clientId,
            name = request.Name,
            description = request.Description,
            enabled = true,
            serviceAccountsEnabled = true,
            standardFlowEnabled = false,        // No browser login
            directAccessGrantsEnabled = false,  // No password grant
            publicClient = false,               // Confidential client
            defaultClientScopes = request.Scopes.ToList(),
            attributes = new Dictionary<string, string>
            {
                ["tenant_id"] = tenantId.Value.ToString()
            }
        };

        HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(
            $"/admin/realms/{_realm}/clients",
            clientRepresentation,
            ct);
        createResponse.EnsureSuccessStatusCode();

        // Get the client's internal ID from the Location header
        string? locationHeader = createResponse.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(locationHeader))
        {
            throw new InvalidOperationException("Client created but Location header is missing");
        }
        string internalClientId = locationHeader.Split('/').Last();

        // Get the generated client secret
        HttpResponseMessage secretResponse = await _httpClient.GetAsync(
            $"/admin/realms/{_realm}/clients/{internalClientId}/client-secret",
            ct);
        secretResponse.EnsureSuccessStatusCode();
        ClientSecretResponse? secretData = await secretResponse.Content.ReadFromJsonAsync<ClientSecretResponse>(ct);
        string clientSecret = secretData?.Value ?? throw new InvalidOperationException("Failed to retrieve client secret");

        // Store local metadata
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            tenantId,
            clientId,
            request.Name,
            request.Description,
            request.Scopes,
            _currentUserService.UserId ?? Guid.Empty,
            _timeProvider);

        _repository.Add(metadata);
        await _repository.SaveChangesAsync(ct);

        LogServiceAccountCreated(clientId, metadata.Id);

        string tokenEndpoint = $"{_keycloakOptions.AuthServerUrl}/realms/{_realm}/protocol/openid-connect/token";

        return new ServiceAccountCreatedResult(
            metadata.Id,
            clientId,
            clientSecret,
            tokenEndpoint,
            request.Scopes.ToList());
    }

    public async Task<IReadOnlyList<ServiceAccountDto>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ServiceAccountMetadata> accounts = await _repository.GetAllAsync(ct);

        return accounts
            .Select(a => new ServiceAccountDto(
                a.Id,
                a.KeycloakClientId,
                a.Name,
                a.Description,
                a.Status,
                a.Scopes,
                a.CreatedAt,
                a.LastUsedAt))
            .ToList();
    }

    public async Task<ServiceAccountDto?> GetAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata? account = await _repository.GetByIdAsync(id, ct);
        if (account is null)
        {
            return null;
        }

        return new ServiceAccountDto(
            account.Id,
            account.KeycloakClientId,
            account.Name,
            account.Description,
            account.Status,
            account.Scopes,
            account.CreatedAt,
            account.LastUsedAt);
    }

    public async Task<SecretRotatedResult> RotateSecretAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await _repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        LogRotatingSecret(metadata.KeycloakClientId);

        // First, get the client's internal ID
        string internalClientId = await GetKeycloakClientInternalIdAsync(metadata.KeycloakClientId, ct);

        // Regenerate the client secret in Keycloak
        HttpResponseMessage response = await _httpClient.PostAsync(
            $"/admin/realms/{_realm}/clients/{internalClientId}/client-secret",
            null,
            ct);
        response.EnsureSuccessStatusCode();

        ClientSecretResponse? secretData = await response.Content.ReadFromJsonAsync<ClientSecretResponse>(ct);
        string newSecret = secretData?.Value ?? throw new InvalidOperationException("Failed to regenerate client secret");

        LogSecretRotated(metadata.KeycloakClientId);

        return new SecretRotatedResult(newSecret, DateTime.UtcNow);
    }

    public async Task UpdateScopesAsync(ServiceAccountMetadataId id, IEnumerable<string> scopes, CancellationToken ct = default)
    {
        List<string> scopesList = scopes.ToList();

        ServiceAccountMetadata? metadata = await _repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        LogUpdatingScopes(metadata.KeycloakClientId);

        // Update in Keycloak
        string internalClientId = await GetKeycloakClientInternalIdAsync(metadata.KeycloakClientId, ct);

        var updatePayload = new
        {
            defaultClientScopes = scopesList
        };

        HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
            $"/admin/realms/{_realm}/clients/{internalClientId}",
            updatePayload,
            ct);
        response.EnsureSuccessStatusCode();

        // Update local metadata
        metadata.UpdateScopes(scopesList, _currentUserService.UserId ?? Guid.Empty, _timeProvider);
        await _repository.SaveChangesAsync(ct);

        LogScopesUpdated(metadata.KeycloakClientId);
    }

    public async Task RevokeAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await _repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        LogRevokingServiceAccount(metadata.KeycloakClientId);

        // Delete from Keycloak
        string internalClientId = await GetKeycloakClientInternalIdAsync(metadata.KeycloakClientId, ct);

        HttpResponseMessage response = await _httpClient.DeleteAsync(
            $"/admin/realms/{_realm}/clients/{internalClientId}",
            ct);
        response.EnsureSuccessStatusCode();

        // Soft delete locally
        metadata.Revoke(_currentUserService.UserId ?? Guid.Empty, _timeProvider);
        await _repository.SaveChangesAsync(ct);

        LogServiceAccountRevoked(metadata.KeycloakClientId);
    }

    private async Task<string> GetKeycloakClientInternalIdAsync(string clientId, CancellationToken ct)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"/admin/realms/{_realm}/clients?clientId={clientId}",
            ct);
        response.EnsureSuccessStatusCode();

        List<KeycloakClientResponse>? clients = await response.Content.ReadFromJsonAsync<List<KeycloakClientResponse>>(ct);
        KeycloakClientResponse? client = clients?.FirstOrDefault();

        if (client?.Id is null)
        {
            throw new InvalidOperationException($"Keycloak client '{clientId}' not found");
        }

        return client.Id;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SlugifyRegex();

    private static string Slugify(string name)
        => SlugifyRegex().Replace(name.ToLowerInvariant(), "-").Trim('-');

    private sealed record ClientSecretResponse(string? Value);
    private sealed record KeycloakClientResponse(string? Id, string? ClientId);
}

public sealed partial class KeycloakServiceAccountService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Creating service account {ClientId} for tenant {TenantId}")]
    private partial void LogCreatingServiceAccount(string clientId, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service account {ClientId} created with ID {Id}")]
    private partial void LogServiceAccountCreated(string clientId, ServiceAccountMetadataId id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rotating secret for service account {ClientId}")]
    private partial void LogRotatingSecret(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Secret rotated for service account {ClientId}")]
    private partial void LogSecretRotated(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating scopes for service account {ClientId}")]
    private partial void LogUpdatingScopes(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scopes updated for service account {ClientId}")]
    private partial void LogScopesUpdated(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoking service account {ClientId}")]
    private partial void LogRevokingServiceAccount(string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Service account {ClientId} revoked")]
    private partial void LogServiceAccountRevoked(string clientId);
}
