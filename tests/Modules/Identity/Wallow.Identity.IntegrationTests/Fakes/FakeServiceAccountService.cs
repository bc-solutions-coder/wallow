using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.IntegrationTests.Fakes;

/// <summary>
/// Fake implementation of IServiceAccountService for testing.
/// Does not make real HTTP calls to an external IdP.
/// </summary>
public sealed class FakeServiceAccountService(IServiceAccountRepository repository, ITenantContext tenantContext) : IServiceAccountService
{

    public async Task<ServiceAccountCreatedResult> CreateAsync(
        CreateServiceAccountRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException(
                "Identity.ServiceAccountNameRequired",
                "Service account name is required");
        }

        Shared.Kernel.Identity.TenantId tenantId = tenantContext.TenantId;
        string clientId = $"sa-{tenantId.Value.ToString()[..8]}-{Slugify(request.Name)}";
        string clientSecret = GenerateFakeSecret();

        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            tenantId,
            clientId,
            request.Name,
            request.Description,
            request.Scopes, Guid.Empty, TimeProvider.System);

        repository.Add(metadata);
        await repository.SaveChangesAsync(ct);

        return new ServiceAccountCreatedResult(
            metadata.Id,
            clientId,
            clientSecret,
            "http://localhost:8080/realms/wallow/protocol/openid-connect/token",
            request.Scopes.ToList());
    }

    public async Task<IReadOnlyList<ServiceAccountDto>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ServiceAccountMetadata> accounts = await repository.GetAllAsync(ct);

        return accounts
            .Select(a => new ServiceAccountDto(
                a.Id,
                a.ClientId,
                a.Name,
                a.Description,
                a.Status,
                a.Scopes,
                a.CreatedAt,
                a.LastUsedAt))
            .ToList();
    }

    public async Task<ServiceAccountDto?> GetAsync(
        ServiceAccountMetadataId id,
        CancellationToken ct = default)
    {
        ServiceAccountMetadata? account = await repository.GetByIdAsync(id, ct);
        if (account is null)
        {
            return null;
        }

        return new ServiceAccountDto(
            account.Id,
            account.ClientId,
            account.Name,
            account.Description,
            account.Status,
            account.Scopes,
            account.CreatedAt,
            account.LastUsedAt);
    }

    public async Task<SecretRotatedResult> RotateSecretAsync(
        ServiceAccountMetadataId id,
        CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        return new SecretRotatedResult(GenerateFakeSecret(), DateTime.UtcNow);
    }

    public async Task UpdateScopesAsync(
        ServiceAccountMetadataId id,
        IEnumerable<string> scopes,
        CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        metadata.UpdateScopes(scopes, Guid.Empty, TimeProvider.System);
        await repository.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(ServiceAccountMetadataId id, CancellationToken ct = default)
    {
        ServiceAccountMetadata? metadata = await repository.GetByIdAsync(id, ct);
        if (metadata is null)
        {
            throw new EntityNotFoundException(nameof(ServiceAccountMetadata), id.Value);
        }

        metadata.Revoke(Guid.Empty, TimeProvider.System);
        await repository.SaveChangesAsync(ct);
    }

    private static string Slugify(string name)
        => System.Text.RegularExpressions.Regex
            .Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-")
            .Trim('-');

    private static string GenerateFakeSecret()
        => $"fake_secret_{Guid.NewGuid():N}";
}
