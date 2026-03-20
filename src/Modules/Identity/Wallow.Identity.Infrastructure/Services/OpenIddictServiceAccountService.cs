using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Services;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

public sealed partial class OpenIddictServiceAccountService(
    IOpenIddictApplicationManager applicationManager,
    IServiceAccountRepository repository,
    ITenantContext tenantContext,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider,
    ILogger<OpenIddictServiceAccountService> logger) : IServiceAccountService
{
    public async Task<ServiceAccountCreatedResult> CreateAsync(CreateServiceAccountRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = tenantContext.TenantId;
        string clientId = $"sa-{tenantId.Value.ToString()[..8]}-{Slugify(request.Name)}";

        LogCreatingServiceAccount(clientId, tenantId.Value);

        string clientSecret = GenerateClientSecret();

        List<string> permissions =
        [
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials
        ];

        foreach (string scope in request.Scopes)
        {
            permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = request.Name,
            ClientType = ClientTypes.Confidential,
            Permissions = { }
        };

        foreach (string permission in permissions)
        {
            descriptor.Permissions.Add(permission);
        }

        await applicationManager.CreateAsync(descriptor, ct);

        // Store local metadata
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            tenantId,
            clientId,
            request.Name,
            request.Description,
            request.Scopes,
            currentUserService.UserId ?? Guid.Empty,
            timeProvider);

        repository.Add(metadata);
        await repository.SaveChangesAsync(ct);

        LogServiceAccountCreated(clientId, metadata.Id);

        return new ServiceAccountCreatedResult(
            metadata.Id,
            clientId,
            clientSecret,
            "/connect/token",
            request.Scopes.ToList());
    }

    public async Task<IReadOnlyList<ServiceAccountDto>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ServiceAccountMetadata> accounts = await repository.GetAllAsync(ct);

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
        ServiceAccountMetadata? account = await repository.GetByIdAsync(id, ct);
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
        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        LogRotatingSecret(metadata.KeycloakClientId);

        object? application = await applicationManager.FindByClientIdAsync(metadata.KeycloakClientId, ct)
            ?? throw new InvalidOperationException($"OpenIddict application '{metadata.KeycloakClientId}' not found");

        string newSecret = GenerateClientSecret();

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        descriptor.ClientSecret = newSecret;
        await applicationManager.UpdateAsync(application, descriptor, ct);

        LogSecretRotated(metadata.KeycloakClientId);

        return new SecretRotatedResult(newSecret, DateTime.UtcNow);
    }

    public async Task UpdateScopesAsync(ServiceAccountMetadataId id, IEnumerable<string> scopes, CancellationToken ct = default)
    {
        List<string> scopesList = scopes.ToList();

        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        LogUpdatingScopes(metadata.KeycloakClientId);

        object? application = await applicationManager.FindByClientIdAsync(metadata.KeycloakClientId, ct)
            ?? throw new InvalidOperationException($"OpenIddict application '{metadata.KeycloakClientId}' not found");

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        // Remove existing scope permissions and re-add with new scopes
        descriptor.Permissions.RemoveWhere(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal));
        foreach (string scope in scopesList)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        await applicationManager.UpdateAsync(application, descriptor, ct);

        // Update local metadata
        metadata.UpdateScopes(scopesList, currentUserService.UserId ?? Guid.Empty, timeProvider);
        await repository.SaveChangesAsync(ct);

        LogScopesUpdated(metadata.KeycloakClientId);
    }

    public async Task RevokeAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        LogRevokingServiceAccount(metadata.KeycloakClientId);

        object? application = await applicationManager.FindByClientIdAsync(metadata.KeycloakClientId, ct);
        if (application is not null)
        {
            await applicationManager.DeleteAsync(application, ct);
        }

        // Soft delete locally
        metadata.Revoke(currentUserService.UserId ?? Guid.Empty, timeProvider);
        await repository.SaveChangesAsync(ct);

        LogServiceAccountRevoked(metadata.KeycloakClientId);
    }

    private static string GenerateClientSecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SlugifyRegex();

    private static string Slugify(string name)
        => SlugifyRegex().Replace(name.ToLowerInvariant(), "-").Trim('-');

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
