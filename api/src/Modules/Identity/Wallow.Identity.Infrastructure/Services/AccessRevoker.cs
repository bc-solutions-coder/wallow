using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Revokes credentials by organization-stamped authorization, client binding, or session ID.
/// Realtime disconnection is a separate call because revoking tokens does not close streams.
/// Session-only revocation does not disconnect realtime streams here.
/// </summary>
public sealed partial class AccessRevoker(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IRegisteredClientRepository registeredClients,
    IMembershipRepository membershipRepository,
    IRealtimeAccessRevoker realtimeAccessRevoker,
    ILogger<AccessRevoker> logger) : IAccessRevoker
{
    public async Task RevokeMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        string subject = userId.ToString();
        HashSet<string> revokedTokenIds = [];

        await RevokeByAuthorizationAsync(subject, organizationId, revokedTokenIds, ct);
        await RevokeByClientBindingAsync(subject, organizationId, revokedTokenIds, ct);
        await realtimeAccessRevoker.RevokeAsync(subject, organizationId, ct);

        LogMembershipAccessRevoked(userId, organizationId, revokedTokenIds.Count);
    }

    public async Task RevokeSessionAsync(Guid userId, string sessionId, CancellationToken ct = default)
    {
        string subject = userId.ToString();
        HashSet<string> revokedTokenIds = [];

        await foreach (object authorization in authorizationManager.FindBySubjectAsync(subject, ct))
        {
            OpenIddictAuthorizationDescriptor descriptor = new();
            await authorizationManager.PopulateAsync(descriptor, authorization, ct);

            if (!string.Equals(descriptor.GetSessionId(), sessionId, StringComparison.Ordinal))
            {
                continue;
            }

            await RevokeAuthorizationWithTokensAsync(authorization, revokedTokenIds, ct);
        }

        LogSessionAccessRevoked(userId, sessionId, revokedTokenIds.Count);
    }

    public async Task RevokeUserAsync(Guid userId, CancellationToken ct = default)
    {
        string subject = userId.ToString();
        HashSet<string> revokedTokenIds = [];

        // Preserve permanent consent while revoking per-login authorizations.
        await foreach (object authorization in authorizationManager.FindBySubjectAsync(subject, ct))
        {
            string? type = await authorizationManager.GetTypeAsync(authorization, ct);
            if (!type.IsAdHocAuthorizationType())
            {
                continue;
            }

            await RevokeAuthorizationWithTokensAsync(authorization, revokedTokenIds, ct);
        }

        // Also cover tokens attached to permanent consent or to no authorization.
        await foreach (object token in tokenManager.FindBySubjectAsync(subject, ct))
        {
            await RevokeTokenAsync(token, revokedTokenIds, ct);
        }

        IReadOnlyList<Membership> memberships = await membershipRepository.GetForUserAsync(userId, ct);
        foreach (Membership membership in memberships.Where(m => m.IsActive))
        {
            await realtimeAccessRevoker.RevokeAsync(subject, membership.OrganizationId.Value, ct);
        }

        LogUserAccessRevoked(userId, revokedTokenIds.Count);
    }

    public async Task<int> RevokeClientAsync(string clientId, CancellationToken ct = default)
    {
        object? application = await applicationManager.FindByClientIdAsync(clientId, ct);
        string? applicationId = application is null ? null : await applicationManager.GetIdAsync(application, ct);
        if (applicationId is null)
        {
            return 0;
        }

        int revoked = 0;
        await foreach (object token in tokenManager.FindByApplicationIdAsync(applicationId, ct))
        {
            if (await tokenManager.TryRevokeAsync(token, ct))
            {
                revoked++;
            }
        }

        await realtimeAccessRevoker.RevokeClientAsync(clientId, ct);

        LogClientAccessRevoked(clientId, revoked);
        return revoked;
    }

    public async Task RevokeOrganizationAsync(Guid organizationId, CancellationToken ct = default)
    {
        IReadOnlyList<RegisteredClient> boundClients =
            await registeredClients.ListByOrganizationAsync(organizationId, ct);
        foreach (RegisteredClient client in boundClients)
        {
            await RevokeClientAsync(client.ClientId, ct);
        }

        IReadOnlyList<Membership> memberships =
            await membershipRepository.GetForOrganizationAsync(organizationId, null, ct);
        foreach (Membership membership in memberships)
        {
            await RevokeMembershipAsync(membership.UserId, organizationId, ct);
        }

        LogOrganizationAccessRevoked(organizationId, boundClients.Count, memberships.Count);
    }

    private async Task RevokeByAuthorizationAsync(
        string subject,
        Guid organizationId,
        HashSet<string> revokedTokenIds,
        CancellationToken ct)
    {
        await foreach (object authorization in authorizationManager.FindBySubjectAsync(subject, ct))
        {
            OpenIddictAuthorizationDescriptor descriptor = new();
            await authorizationManager.PopulateAsync(descriptor, authorization, ct);

            if (descriptor.GetOrganizationId() != organizationId)
            {
                continue;
            }

            await RevokeAuthorizationWithTokensAsync(authorization, revokedTokenIds, ct);
        }
    }

    private async Task RevokeAuthorizationWithTokensAsync(
        object authorization,
        HashSet<string> revokedTokenIds,
        CancellationToken ct)
    {
        string? authorizationId = await authorizationManager.GetIdAsync(authorization, ct);
        if (authorizationId is not null)
        {
            await foreach (object token in tokenManager.FindByAuthorizationIdAsync(authorizationId, ct))
            {
                await RevokeTokenAsync(token, revokedTokenIds, ct);
            }
        }

        await authorizationManager.TryRevokeAsync(authorization, ct);
    }

    private async Task RevokeByClientBindingAsync(
        string subject,
        Guid organizationId,
        HashSet<string> revokedTokenIds,
        CancellationToken ct)
    {
        Dictionary<string, bool> clientBelongsToOrganization = [];

        await foreach (object token in tokenManager.FindBySubjectAsync(subject, ct))
        {
            string? applicationId = await tokenManager.GetApplicationIdAsync(token, ct);

            if (applicationId is null)
            {
                continue;
            }

            if (!clientBelongsToOrganization.TryGetValue(applicationId, out bool belongs))
            {
                belongs = await BelongsToOrganizationAsync(applicationId, organizationId, ct);
                clientBelongsToOrganization[applicationId] = belongs;
            }

            if (belongs)
            {
                await RevokeTokenAsync(token, revokedTokenIds, ct);
            }
        }
    }

    private async Task RevokeTokenAsync(object token, HashSet<string> revokedTokenIds, CancellationToken ct)
    {
        string? tokenId = await tokenManager.GetIdAsync(token, ct);
        if (tokenId is not null && !revokedTokenIds.Add(tokenId))
        {
            return;
        }

        await tokenManager.TryRevokeAsync(token, ct);
    }

    private async Task<bool> BelongsToOrganizationAsync(
        string applicationId,
        Guid organizationId,
        CancellationToken ct)
    {
        object? application = await applicationManager.FindByIdAsync(applicationId, ct);

        if (application is null)
        {
            return false;
        }

        OpenIddictApplicationDescriptor descriptor = new();
        await applicationManager.PopulateAsync(descriptor, application, ct);

        return Guid.TryParse(descriptor.GetTenantId(), out Guid clientOrganizationId)
            && clientOrganizationId == organizationId;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked access for user {UserId} in organization {OrganizationId}: {RevokedTokenCount} tokens")]
    private partial void LogMembershipAccessRevoked(Guid userId, Guid organizationId, int revokedTokenCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked session {SessionId} of user {UserId}: {RevokedTokenCount} tokens")]
    private partial void LogSessionAccessRevoked(Guid userId, string sessionId, int revokedTokenCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked all access of user {UserId}: {RevokedTokenCount} tokens")]
    private partial void LogUserAccessRevoked(Guid userId, int revokedTokenCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked access of client {ClientId}: {RevokedTokenCount} tokens")]
    private partial void LogClientAccessRevoked(string clientId, int revokedTokenCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked access across organization {OrganizationId}: {ClientCount} bound clients, {MemberCount} members")]
    private partial void LogOrganizationAccessRevoked(Guid organizationId, int clientCount, int memberCount);
}
