using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.Domain;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Registers and manages the clients an organization owns. A registration writes two records in
/// one transaction — the OpenIddict application carrying the OAuth configuration and the
/// <see cref="RegisteredClient"/> row carrying what OpenIddict has no place for — and hands back the
/// only copy of the client secret the caller will ever see.
/// </summary>
public sealed partial class OrganizationClientService(
    IOpenIddictApplicationManager applicationManager,
    IRegisteredClientRepository registeredClients,
    IdentityDbContext dbContext,
    IOrganizationRepository organizations,
    IApiScopeRepository apiScopes,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IOptions<ServiceUrlsOptions> serviceUrls,
    ILogger<OrganizationClientService> logger) : IOrganizationClientService
{
    private const int ClientSecretBytes = 32;

    public async Task<OrganizationClientRegistrationResult> RegisterApplicationAsync(
        Guid organizationId,
        RegisterApplicationInput input,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        Organization organization = await organizations.GetByIdAsync(OrganizationId.Create(organizationId), ct)
            ?? throw new EntityNotFoundException("Organization", organizationId);

        await EnsureGrantableAsync(input.Configuration.Scopes, ct);

        string clientId = ClientIdDerivation.DeriveApplicationClientId(organization.Slug, input.Name);
        if (await applicationManager.FindByClientIdAsync(clientId, ct) is not null)
        {
            throw ClientIdTaken(input.Name, clientId);
        }

        string clientSecret = GenerateClientSecret();
        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = input.Name,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Explicit,
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        descriptor.SetTenantId(organizationId.ToString());
        ApplyConfiguration(descriptor, input.Configuration);

        RegisteredClient record = RegisteredClient.Create(
            clientId, organizationId, RegisteredClientKind.Application, actorUserId, timeProvider);

        // Both writes land on IdentityDbContext (OpenIddict's store shares it), so one transaction
        // covers them: no application without its record, no record without its application. The
        // execution strategy wraps the transaction because the context retries on transient faults.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(
                ct,
                async token =>
                {
                    await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                    await applicationManager.CreateAsync(descriptor, token);
                    registeredClients.Add(record);
                    await registeredClients.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);
                });
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent registration of the same name got past the lookup above; the unique
            // index on the client id is what actually decides, so answer as the lookup would have.
            throw ClientIdTaken(input.Name, clientId);
        }

        LogApplicationRegistered(clientId, organizationId, actorUserId);

        return new OrganizationClientRegistrationResult(
            ToDto(record, descriptor),
            clientSecret,
            ResolveIssuer(),
            TrimmedOrNull(serviceUrls.Value.ApiUrl));
    }

    public async Task<IReadOnlyList<OrganizationClientDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        IReadOnlyList<RegisteredClient> records = await registeredClients.ListByOrganizationAsync(organizationId, ct);
        List<OrganizationClientDto> result = new(records.Count);
        foreach (RegisteredClient record in records)
        {
            OpenIddictApplicationDescriptor? descriptor = await DescriptorOfAsync(record, ct);
            if (descriptor is not null)
            {
                result.Add(ToDto(record, descriptor));
            }
        }

        return result;
    }

    public async Task<OrganizationClientDto?> GetAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null)
        {
            return null;
        }

        OpenIddictApplicationDescriptor? descriptor = await DescriptorOfAsync(record, ct);
        return descriptor is null ? null : ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> UpdateAsync(
        Guid organizationId,
        string clientId,
        ClientConfigurationInput configuration,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null)
        {
            return null;
        }

        object? application = await applicationManager.FindByClientIdAsync(record.ClientId, ct);
        if (application is null)
        {
            LogApplicationMissing(record.ClientId);
            return null;
        }

        await EnsureGrantableAsync(configuration.Scopes, ct);

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        descriptor.RedirectUris.Clear();
        descriptor.PostLogoutRedirectUris.Clear();
        descriptor.Permissions.RemoveWhere(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal));
        ApplyConfiguration(descriptor, configuration);

        await applicationManager.UpdateAsync(application, descriptor, ct);
        return ToDto(record, descriptor);
    }

    public async Task<bool> DeleteAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null)
        {
            return false;
        }

        object? application = await applicationManager.FindByClientIdAsync(record.ClientId, ct);
        if (application is not null)
        {
            await applicationManager.DeleteAsync(application, ct);
        }

        registeredClients.Remove(record);
        await registeredClients.SaveChangesAsync(ct);
        LogApplicationDeleted(record.ClientId, organizationId);
        return true;
    }

    private async Task<RegisteredClient?> OwnedRecordAsync(Guid organizationId, string clientId, CancellationToken ct)
    {
        RegisteredClient? record = await registeredClients.GetByClientIdAsync(clientId, ct);
        return record is not null && record.OrganizationId == organizationId ? record : null;
    }

    private async Task<OpenIddictApplicationDescriptor?> DescriptorOfAsync(RegisteredClient record, CancellationToken ct)
    {
        object? application = await applicationManager.FindByClientIdAsync(record.ClientId, ct);
        if (application is null)
        {
            LogApplicationMissing(record.ClientId);
            return null;
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        return descriptor;
    }

    /// <summary>
    /// A developer application may hold the OIDC login scopes and any catalog scope that is not
    /// reserved for the platform's own clients. Nothing outside the catalog is grantable.
    /// </summary>
    private async Task EnsureGrantableAsync(IReadOnlyList<string> requested, CancellationToken ct)
    {
        List<string> apiScopeCodes = requested.Where(s => !ApiScopes.LoginScopes.Contains(s)).ToList();
        if (apiScopeCodes.Count == 0)
        {
            return;
        }

        IReadOnlyList<ApiScope> known = await apiScopes.GetByCodesAsync(apiScopeCodes, ct);
        List<string> unknown = apiScopeCodes
            .Where(code => known.All(s => !string.Equals(s.Code, code, StringComparison.Ordinal)))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException(
                "Identity.UnknownScope",
                $"Unknown scopes: {string.Join(", ", unknown)}.");
        }

        List<string> platformOnly = known.Where(s => s.PlatformOnly).Select(s => s.Code).ToList();
        if (platformOnly.Count > 0)
        {
            throw new BusinessRuleException(
                "Identity.PlatformOnlyScope",
                $"Scopes reserved for the platform's own clients cannot be granted to an application: {string.Join(", ", platformOnly)}.");
        }
    }

    private static void ApplyConfiguration(OpenIddictApplicationDescriptor descriptor, ClientConfigurationInput configuration)
    {
        foreach (Uri uri in configuration.RedirectUris)
        {
            descriptor.RedirectUris.Add(uri);
        }

        foreach (Uri uri in configuration.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(uri);
        }

        descriptor.SetBackchannelLogoutUri(configuration.BackchannelLogoutUri);

        // Without these the client is refused every scope it asks for on its first authorize:
        // OpenIddict grants only what the application's own permissions list allows.
        foreach (string scope in configuration.Scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }
    }

    private static BusinessRuleException ClientIdTaken(string name, string clientId) =>
        new(
            "Identity.ClientIdTaken",
            $"An application named '{name}' already exists in this organization (client id '{clientId}').");

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static OrganizationClientDto ToDto(RegisteredClient record, OpenIddictApplicationDescriptor descriptor) =>
        new(
            record.ClientId,
            descriptor.DisplayName ?? record.ClientId,
            record.Kind,
            record.Status,
            descriptor.RedirectUris.Select(u => u.AbsoluteUri).ToList(),
            descriptor.PostLogoutRedirectUris.Select(u => u.AbsoluteUri).ToList(),
            descriptor.GetBackchannelLogoutUri()?.AbsoluteUri,
            descriptor.Permissions
                .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
                .Select(p => p[Permissions.Prefixes.Scope.Length..])
                .ToList(),
            record.CreatedByUserId,
            record.CreatedAt,
            record.LastUsedAt);

    /// <summary>
    /// The issuer the application must validate tokens against: the public auth URL including any
    /// path prefix, exactly as OpenIddict advertises it in the discovery document.
    /// </summary>
    private string? ResolveIssuer()
    {
        Uri? issuer = OpenIddictIssuerResolver.Resolve(configuration);
        return TrimmedOrNull(issuer?.AbsoluteUri ?? serviceUrls.Value.AuthUrl);
    }

    private static string? TrimmedOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');

    private static string GenerateClientSecret()
    {
        Span<byte> bytes = stackalloc byte[ClientSecretBytes];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered application {ClientId} for organization {OrganizationId} by {UserId}")]
    private partial void LogApplicationRegistered(string clientId, Guid organizationId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted application {ClientId} of organization {OrganizationId}")]
    private partial void LogApplicationDeleted(string clientId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registered client {ClientId} has no OpenIddict application")]
    private partial void LogApplicationMissing(string clientId);
}
