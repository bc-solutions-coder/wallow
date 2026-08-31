using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Revocation walks OpenIddict's token index and ends what it finds, then hangs up the realtime
/// connections that already-issued tokens keep open — revoking a token says nothing to a socket
/// that is already open, and an open stream carries the roles it was opened with.
///
/// By membership, a token names its organization in one of two places. A bound client's tokens
/// carry it through the client: the binding is what put an org_id on them. A first-party client
/// is bound to none, so its sign-in writes the organization on the authorization its tokens chain
/// to instead. Revoking access to one organization means revoking the subject's tokens found
/// either way, and no others. By client, every token names the application it was issued to, so
/// the whole revocation is one walk over that index. By session, every sign-in stamps its
/// per-login authorization with the session's sid, so end-session revokes exactly the one
/// browser session's tokens and no others.
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

        // Per-login authorizations die with the account; the permanent consent records survive,
        // so a reactivated user signs back in without re-consenting.
        await foreach (object authorization in authorizationManager.FindBySubjectAsync(subject, ct))
        {
            string? type = await authorizationManager.GetTypeAsync(authorization, ct);
            if (!type.IsAdHocAuthorizationType())
            {
                continue;
            }

            await RevokeAuthorizationWithTokensAsync(authorization, revokedTokenIds, ct);
        }

        // The subject walk catches what the authorization walk cannot see: tokens chained to a
        // consent record and tokens chained to nothing.
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
