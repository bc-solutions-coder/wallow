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
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts;
using Wallow.Shared.Contracts.Identity;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Configuration;
using Wallow.Shared.Kernel.Domain;
using Wolverine.EntityFrameworkCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Manages organization-owned applications and service accounts. Registration writes
/// the OpenIddict application and <see cref="RegisteredClient"/> in one transaction,
/// with the organization binding used by token issuance. The returned secret is not readable later.
/// </summary>
public sealed partial class OrganizationClientService(
    IOpenIddictApplicationManager applicationManager,
    IRegisteredClientRepository registeredClients,
    IAccessRevoker accessRevoker,
    IdentityDbContext dbContext,
    IDbContextOutbox outbox,
    IOrganizationRepository organizations,
    IOrganizationAdminEmailResolver adminEmails,
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
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(actor);

        Organization organization = await organizations.GetByIdAsync(OrganizationId.Create(organizationId), ct)
            ?? throw new EntityNotFoundException(IdentityErrors.OrganizationNotFound, organizationId);

        await EnsureGrantableAsync(input.Configuration.Scopes, ct);

        string clientId = ClientIdDerivation.DeriveClientId(input.Kind, organization.Slug, input.Name);
        if (await applicationManager.FindByClientIdAsync(clientId, ct) is not null)
        {
            throw ClientIdTaken(input.Name, clientId);
        }

        string clientSecret = GenerateClientSecret();

        // Branding manages the mutable display name; RegisteredClient retains the registration name.
        string displayName = (input.BrandingDisplayName ?? input.Name).Trim();
        OpenIddictApplicationDescriptor descriptor = NewDescriptor(input.Kind, clientId, clientSecret, displayName);
        descriptor.SetTenantId(organizationId.ToString());
        ApplyConfiguration(descriptor, input.Kind, input.Configuration);

        // Applications receive an explicit third-party default; service accounts have no refresh grant.
        if (input.Configuration.RefreshTokenLifetime is null && input.Kind == RegisteredClientKind.Application)
        {
            descriptor.SetRefreshTokenLifetime(ClientRefreshTokenLifetimes.ThirdPartyDefaultSeconds);
        }

        RegisteredClient record = RegisteredClient.Create(
            clientId, organizationId, input.Name, input.Kind, actor.ActorId, timeProvider);

        // Application, registration and branding event share the Identity transaction/outbox.
        try
        {
            await CommitAndPublishAsync(
                new ClientRegisteredEvent
                {
                    ClientId = clientId,
                    OrganizationId = organizationId,
                    ClientName = input.Name,
                    Kind = input.Kind == RegisteredClientKind.Application
                        ? OrganizationClientKind.Application
                        : OrganizationClientKind.ServiceAccount,
                    ActorId = actor.ActorId,
                    BrandingDisplayName = input.BrandingDisplayName,
                    BrandingTagline = input.BrandingTagline,
                    IpAddress = actor.IpAddress,
                },
                async token =>
                {
                    await applicationManager.CreateAsync(descriptor, token);
                    registeredClients.Add(record);
                    await registeredClients.SaveChangesAsync(token);
                },
                ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Translate a uniqueness failure to the same client-ID conflict as the pre-check.
            throw ClientIdTaken(input.Name, clientId);
        }

        LogClientRegistered(clientId, organizationId, actor.ActorId);

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
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

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
        // Pass plaintext to the application manager for hashing.
        descriptor.ClientSecret = clientSecret;
        record.RecordSecretRotation(actor.ActorId, timeProvider);

        // Commit secret replacement, rotation provenance and optional token revocation together.
        await CommitAndPublishAsync(
            new ClientSecretRotatedEvent
            {
                ClientId = record.ClientId,
                OrganizationId = organizationId,
                ActorId = actor.ActorId,
                ActiveTokensRevoked = revokeActiveTokens,
                IpAddress = actor.IpAddress,
            },
            async token =>
            {
                await applicationManager.UpdateAsync(application, descriptor, token);
                await registeredClients.SaveChangesAsync(token);
                if (revokeActiveTokens)
                {
                    await accessRevoker.RevokeClientAsync(record.ClientId, token);
                }
            },
            ct);

        LogClientSecretRotated(record.ClientId, organizationId, actor.ActorId, revokeActiveTokens);

        return new OrganizationClientRegistrationResult(
            ToDto(record, descriptor),
            clientSecret,
            ResolveIssuer(),
            TrimmedOrNull(serviceUrls.Value.ApiUrl));
    }

    public async Task<OrganizationClientDto?> SuspendAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.Suspend();

        // Keep database status and token revocation in one transaction.
        await CommitAndPublishAsync(
            new ClientSuspendedEvent
            {
                ClientId = record.ClientId,
                OrganizationId = organizationId,
                ActorId = actor.ActorId,
                IpAddress = actor.IpAddress,
            },
            async token =>
            {
                await registeredClients.SaveChangesAsync(token);
                await accessRevoker.RevokeClientAsync(record.ClientId, token);
            },
            ct);

        LogClientSuspended(record.ClientId, organizationId);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> ReinstateAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.Reinstate();
        await CommitAndPublishAsync(
            new ClientReinstatedEvent
            {
                ClientId = record.ClientId,
                OrganizationId = organizationId,
                ActorId = actor.ActorId,
                IpAddress = actor.IpAddress,
            },
            token => registeredClients.SaveChangesAsync(token),
            ct);

        LogClientReinstated(record.ClientId, organizationId);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> SuspendByPlatformAsync(
        Guid organizationId,
        string clientId,
        string reason,
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        // Capture notification recipients and organization name for the outbox event.
        IReadOnlyList<string> recipients = await adminEmails.ResolveAsync(organizationId, ct);
        Organization? organization = await organizations.GetByIdAsync(OrganizationId.Create(organizationId), ct);

        record.SuspendByPlatform(reason, actor.ActorId, timeProvider);

        // Commit platform suspension and database token revocation together.
        await CommitAndPublishAsync(
            new ClientSuspendedByPlatformEvent
            {
                ClientId = record.ClientId,
                ClientName = record.Name,
                OrganizationId = organizationId,
                OrganizationName = organization?.Name ?? string.Empty,
                ActorId = actor.ActorId,
                Reason = record.PlatformSuspensionReason ?? reason,
                RecipientEmails = recipients,
                IpAddress = actor.IpAddress,
            },
            async token =>
            {
                await registeredClients.SaveChangesAsync(token);
                await accessRevoker.RevokeClientAsync(record.ClientId, token);
            },
            ct);

        LogClientSuspendedByPlatform(record.ClientId, organizationId, actor.ActorId);
        return ToDto(record, descriptor);
    }

    public async Task<OrganizationClientDto?> ReinstateByPlatformAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        OpenIddictApplicationDescriptor? descriptor = record is null ? null : await DescriptorOfAsync(record, ct);
        if (record is null || descriptor is null)
        {
            return null;
        }

        record.ReinstateByPlatform();
        await CommitAndPublishAsync(
            new ClientReinstatedByPlatformEvent
            {
                ClientId = record.ClientId,
                OrganizationId = organizationId,
                ActorId = actor.ActorId,
                IpAddress = actor.IpAddress,
            },
            token => registeredClients.SaveChangesAsync(token),
            ct);

        LogClientReinstatedByPlatform(record.ClientId, organizationId);
        return ToDto(record, descriptor);
    }

    public async Task<bool> DeleteAsync(
        Guid organizationId,
        string clientId,
        ClientActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        RegisteredClient? record = await OwnedRecordAsync(organizationId, clientId, ct);
        if (record is null)
        {
            return false;
        }

        object? application = await applicationManager.FindByClientIdAsync(record.ClientId, ct);

        // Request realtime disconnection while the client still exists; then delete its application.
        await CommitAndPublishAsync(
            new ClientDeletedEvent
            {
                ClientId = record.ClientId,
                OrganizationId = organizationId,
                ActorId = actor.ActorId,
                IpAddress = actor.IpAddress,
            },
            async token =>
            {
                await accessRevoker.RevokeClientAsync(record.ClientId, token);

                if (application is not null)
                {
                    RevokedTokenDetacher.DetachRevokedTokens(dbContext, application);
                    await applicationManager.DeleteAsync(application, token);
                }

                registeredClients.Remove(record);
                await registeredClients.SaveChangesAsync(token);
            },
            ct);

        LogClientDeleted(record.ClientId, organizationId);
        return true;
    }

    /// <summary>
    /// Commits writes and the outbox event together, then flushes outgoing messages.
    /// The execution strategy may retry after an ambiguous commit; consumers must tolerate
    /// redelivery. External effects performed by writes cannot roll back with the database.
    /// </summary>
    private async Task CommitAndPublishAsync<TEvent>(
        TEvent @event, Func<CancellationToken, Task> writes, CancellationToken ct)
        where TEvent : IntegrationEvent
    {
        outbox.Enroll(dbContext);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            ct,
            async token =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                await writes(token);
                await outbox.PublishAsync(@event);
                await transaction.CommitAsync(token);
            });
        await outbox.FlushOutgoingMessagesAsync();
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
    /// Accepts login scopes and catalog scopes that are not platform-only.
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
                IdentityErrors.UnknownScope,
                $"Unknown scopes: {string.Join(", ", unknown)}.");
        }

        List<string> platformOnly = known.Where(s => s.PlatformOnly).Select(s => s.Code).ToList();
        if (platformOnly.Count > 0)
        {
            throw new BusinessRuleException(
                IdentityErrors.PlatformOnlyScope,
                $"Scopes reserved for the platform's own clients cannot be granted here: {string.Join(", ", platformOnly)}.");
        }
    }

    /// <summary>
    /// Creates confidential clients: applications get authorization code, refresh and PKCE;
    /// service accounts get client credentials without browser-flow permissions.
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

    /// <summary>
    /// Applies URI settings only to applications; scope and explicit lifetime settings apply to both kinds.
    /// </summary>
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
            descriptor.SetBackchannelLogoutSessionRequired(configuration.BackchannelLogoutSessionRequired);
        }

        // Record the allowed scopes on the application permission list.
        foreach (string scope in configuration.Scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        // Omission preserves the current lifetime; registration selects its own default.
        if (configuration.RefreshTokenLifetime is { } refreshTokenLifetime)
        {
            descriptor.SetRefreshTokenLifetime(refreshTokenLifetime);
        }
    }

    private static BusinessRuleException ClientIdTaken(string name, string clientId) =>
        new(
            IdentityErrors.ClientIdTaken,
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
            descriptor.GetBackchannelLogoutSessionRequired(),
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
    /// Resolves the configured issuer, falling back to the service auth URL.
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
