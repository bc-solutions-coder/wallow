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
/// Registers and manages the clients an organization owns, developer applications and service
/// accounts alike. A registration writes two records in one transaction — the OpenIddict
/// application carrying the OAuth configuration and the <see cref="RegisteredClient"/> row carrying
/// what OpenIddict has no place for — and hands back the only copy of the client secret the caller
/// will ever see. Both kinds are bound to the organization on the OpenIddict application, which is
/// where the token endpoint reads the <c>org_id</c> claim from.
/// </summary>
public sealed partial class OrganizationClientService(
    IOpenIddictApplicationManager applicationManager,
    IRegisteredClientRepository registeredClients,
    IAccessRevoker accessRevoker,
    IdentityDbContext dbContext,
    IOrganizationRepository organizations,
    IApiScopeRepository apiScopes,
    TimeProvider timeProvider,
    IConfiguration configuration,
    IOptions<ServiceUrlsOptions> serviceUrls,
    ILogger<OrganizationClientService> logger) : IOrganizationClientService
{
    private const int ClientSecretBytes = 32;

    public async Task<OrganizationClientRegistrationResult> RegisterAsync(
        Guid organizationId,
        RegisterClientInput input,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        Organization organization = await organizations.GetByIdAsync(OrganizationId.Create(organizationId), ct)
            ?? throw new EntityNotFoundException("Organization", organizationId);

        await EnsureGrantableAsync(input.Configuration.Scopes, ct);

        string clientId = ClientIdDerivation.DeriveClientId(input.Kind, organization.Slug, input.Name);
        if (await applicationManager.FindByClientIdAsync(clientId, ct) is not null)
        {
            throw ClientIdTaken(input.Name, clientId);
        }

        string clientSecret = GenerateClientSecret();

        // The OpenIddict display name is the end-user-facing branded name; Branding owns it after
        // registration. The immutable ledger name lives on the RegisteredClient row instead.
        string displayName = (input.BrandingDisplayName ?? input.Name).Trim();
        OpenIddictApplicationDescriptor descriptor = NewDescriptor(input.Kind, clientId, clientSecret, displayName);
        descriptor.SetTenantId(organizationId.ToString());
        ApplyConfiguration(descriptor, input.Kind, input.Configuration);

        // Every organization-registered client is third-party, so an unstated lifetime is pinned
        // to the third-party default here rather than left to the global fallback. A service
        // account holds no refresh grant, so it gets no lifetime it could never use.
        if (input.Configuration.RefreshTokenLifetime is null && input.Kind == RegisteredClientKind.Application)
        {
            descriptor.SetRefreshTokenLifetime(ClientRefreshTokenLifetimes.ThirdPartyDefaultSeconds);
        }

        RegisteredClient record = RegisteredClient.Create(
            clientId, organizationId, input.Name, input.Kind, actorUserId, timeProvider);

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

        LogClientRegistered(clientId, organizationId, actorUserId);

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
        ApplyConfiguration(descriptor, record.Kind, configuration);

        await applicationManager.UpdateAsync(application, descriptor, ct);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientRegistrationResult?> RotateSecretAsync(
        Guid organizationId,
        string clientId,
        bool revokeActiveTokens,
        Guid actorUserId,
        CancellationToken ct = default)
    {
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

        string clientSecret = GenerateClientSecret();
        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);
        // The manager re-hashes a secret that differs from the stored one, so the descriptor is
        // the only place the plaintext ever sits.
        descriptor.ClientSecret = clientSecret;
        record.RecordSecretRotation(actorUserId, timeProvider);

        // Immediate, with no overlap: the old secret, the provenance and (when asked) every
        // outstanding token change in one transaction, so a compromise response is one step.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await applicationManager.UpdateAsync(application, descriptor, token);
                await registeredClients.SaveChangesAsync(token);
                if (revokeActiveTokens)
                {
                    await accessRevoker.RevokeClientAsync(record.ClientId, token);
                }

                await transaction.CommitAsync(token);
            });

        LogClientSecretRotated(record.ClientId, organizationId, actorUserId, revokeActiveTokens);

        return new OrganizationClientRegistrationResult(
            ToDto(record, descriptor),
            clientSecret,
            ResolveIssuer(),
            TrimmedOrNull(serviceUrls.Value.ApiUrl));
    }

    public async Task<OrganizationClientDto?> SuspendAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.Suspend();

        // The status and the revocation land together: a suspended client with a live token, or
        // a revoked client still marked active, is exactly the half-state the transaction forbids.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await registeredClients.SaveChangesAsync(token);
                await accessRevoker.RevokeClientAsync(record.ClientId, token);
                await transaction.CommitAsync(token);
            });

        LogClientSuspended(record.ClientId, organizationId);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> ReinstateAsync(Guid organizationId, string clientId, CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.Reinstate();
        await registeredClients.SaveChangesAsync(ct);

        LogClientReinstated(record.ClientId, organizationId);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> SuspendByPlatformAsync(
        Guid organizationId,
        string clientId,
        string reason,
        Guid actorId,
        CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.SuspendByPlatform(reason, actorId, timeProvider);

        // Same shape as the organization's own suspend: the mark and the revocation land
        // together, so no window exists where one is visible without the other.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await registeredClients.SaveChangesAsync(token);
                await accessRevoker.RevokeClientAsync(record.ClientId, token);
                await transaction.CommitAsync(token);
            });

        LogClientSuspendedByPlatform(record.ClientId, organizationId, actorId);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> ReinstateByPlatformAsync(
        Guid organizationId,
        string clientId,
        CancellationToken ct = default)
    {
        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.ReinstateByPlatform();
        await registeredClients.SaveChangesAsync(ct);

        LogClientReinstatedByPlatform(record.ClientId, organizationId);
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

        // Revocation first, so the realtime connections are hung up while the client still
        // exists to name them; the application's own tokens and consents then go with it.
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await accessRevoker.RevokeClientAsync(record.ClientId, token);

                if (application is not null)
                {
                    RevokedTokenDetacher.DetachRevokedTokens(dbContext, application);
                    await applicationManager.DeleteAsync(application, token);
                }

                registeredClients.Remove(record);
                await registeredClients.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
            });

        LogClientDeleted(record.ClientId, organizationId);
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
    /// One rule for both kinds: a client may hold the OIDC login scopes and any catalog scope that
    /// is not reserved for the platform's own clients. Nothing outside the catalog is grantable.
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
                $"Scopes reserved for the platform's own clients cannot be granted here: {string.Join(", ", platformOnly)}.");
        }
    }

    /// <summary>
    /// A developer application is a confidential authorization-code client with PKCE; a service
    /// account is a confidential client-credentials client and nothing else, so it can never be
    /// handed a browser's authorize request.
    /// </summary>
    private static OpenIddictApplicationDescriptor NewDescriptor(
        RegisteredClientKind kind, string clientId, string clientSecret, string displayName)
    {
        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = displayName,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Explicit,
            Permissions = { Permissions.Endpoints.Token, Permissions.Endpoints.Revocation },
        };

        if (kind == RegisteredClientKind.ServiceAccount)
        {
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
            return descriptor;
        }

        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        return descriptor;
    }

    /// <summary>A service account ignores every URI field: it has no browser to send anywhere.</summary>
    private static void ApplyConfiguration(
        OpenIddictApplicationDescriptor descriptor, RegisteredClientKind kind, ClientConfigurationInput configuration)
    {
        if (kind == RegisteredClientKind.Application)
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
        }

        // Without these the client is refused every scope it asks for on its first authorize:
        // OpenIddict grants only what the application's own permissions list allows.
        foreach (string scope in configuration.Scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        // Null means "keep the current policy": an update omitting the field must not silently
        // reset a client's lifetime, and registration handles its own default.
        if (configuration.RefreshTokenLifetime is { } refreshTokenLifetime)
        {
            descriptor.SetRefreshTokenLifetime(refreshTokenLifetime);
        }
    }

    private static BusinessRuleException ClientIdTaken(string name, string clientId) =>
        new(
            "Identity.ClientIdTaken",
            $"A client named '{name}' already exists in this organization (client id '{clientId}').");

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static OrganizationClientDto ToDto(RegisteredClient record, OpenIddictApplicationDescriptor descriptor) =>
        new(
            record.ClientId,
            record.Name,
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
            record.LastUsedAt,
            record.LastRotatedByUserId,
            record.LastRotatedAt,
            record.PlatformSuspendedAt,
            record.PlatformSuspensionReason,
            descriptor.GetRefreshTokenLifetimeSeconds());

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered client {ClientId} for organization {OrganizationId} by {UserId}")]
    private partial void LogClientRegistered(string clientId, Guid organizationId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rotated the secret of client {ClientId} of organization {OrganizationId} by {UserId} (active tokens revoked: {ActiveTokensRevoked})")]
    private partial void LogClientSecretRotated(string clientId, Guid organizationId, Guid userId, bool activeTokensRevoked);

    [LoggerMessage(Level = LogLevel.Information, Message = "Suspended client {ClientId} of organization {OrganizationId}")]
    private partial void LogClientSuspended(string clientId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reinstated client {ClientId} of organization {OrganizationId}")]
    private partial void LogClientReinstated(string clientId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Client {ClientId} of organization {OrganizationId} suspended by platform actor {ActorId}")]
    private partial void LogClientSuspendedByPlatform(string clientId, Guid organizationId, Guid actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client {ClientId} of organization {OrganizationId} reinstated by platform")]
    private partial void LogClientReinstatedByPlatform(string clientId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted client {ClientId} of organization {OrganizationId}")]
    private partial void LogClientDeleted(string clientId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registered client {ClientId} has no OpenIddict application")]
    private partial void LogApplicationMissing(string clientId);
}
