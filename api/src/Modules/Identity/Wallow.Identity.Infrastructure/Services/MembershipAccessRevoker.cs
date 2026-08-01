using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Shared.Contracts.Realtime;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// A token belongs to the organization its client is bound to — that binding is what puts an
/// org_id on the token in the first place — so revoking access to one organization means
/// revoking the subject's tokens issued through that organization's clients, and no others.
/// </summary>
public sealed partial class MembershipAccessRevoker(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager,
    IRealtimeAccessRevoker realtimeAccessRevoker,
    ILogger<MembershipAccessRevoker> logger) : IMembershipAccessRevoker
{
    public async Task RevokeAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        string subject = userId.ToString();
        Dictionary<string, bool> clientBelongsToOrganization = [];
        int revoked = 0;

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

            if (belongs && await tokenManager.TryRevokeAsync(token, ct))
            {
                revoked++;
            }
        }

        // Revoking a token says nothing to a socket that is already open, and an open stream
        // carries the roles it was opened with.
        await realtimeAccessRevoker.RevokeAsync(subject, organizationId, ct);

        LogAccessRevoked(userId, organizationId, revoked);
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
