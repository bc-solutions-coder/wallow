using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// A token names its organization in one of two places. A bound client's tokens carry it through
/// the client: the binding is what put an org_id on them. A first-party client is bound to none,
/// so its sign-in writes the organization on the authorization its tokens chain to instead.
/// Revoking access to one organization means revoking the subject's tokens found either way, and
/// no others.
/// </summary>
public sealed partial class MembershipAccessRevoker(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IRealtimeAccessRevoker realtimeAccessRevoker,
    ILogger<MembershipAccessRevoker> logger) : IMembershipAccessRevoker
{
    public async Task RevokeAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        string subject = userId.ToString();
        HashSet<string> revokedTokenIds = [];

        await RevokeByAuthorizationAsync(subject, organizationId, revokedTokenIds, ct);
        await RevokeByClientBindingAsync(subject, organizationId, revokedTokenIds, ct);

        // Revoking a token says nothing to a socket that is already open, and an open stream
        // carries the roles it was opened with.
        await realtimeAccessRevoker.RevokeAsync(subject, organizationId, ct);

        LogAccessRevoked(userId, organizationId, revokedTokenIds.Count);
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
    private partial void LogAccessRevoked(Guid userId, Guid organizationId, int revokedTokenCount);
}
