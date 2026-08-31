using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// The consent ledger a user can read and edit. Connected applications are the user's Valid
/// permanent authorizations — the durable consent records token issuance chains to — never the
/// ad-hoc rows first-party sign-ins leave behind. Withdrawing one revokes the authorization and
/// every token chained to it, so refresh dies with <c>invalid_grant</c> and token-entry
/// validation refuses the surviving access tokens on their next request.
/// </summary>
public sealed partial class ConnectedApplicationService(
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictTokenManager tokenManager,
    ILogger<ConnectedApplicationService> logger) : IConnectedApplicationService
{
    public async Task<IReadOnlyList<ConnectedApplicationDto>> GetConnectedApplicationsAsync(
        Guid userId, CancellationToken ct = default)
    {
        List<ConnectedApplicationDto> connected = [];

        await foreach (object authorization in authorizationManager.FindBySubjectAsync(userId.ToString(), ct))
        {
            if (!await IsPermanentConsentAsync(authorization, ct))
            {
                continue;
            }

            string? applicationId = await authorizationManager.GetApplicationIdAsync(authorization, ct);
            object? application = applicationId is null
                ? null
                : await applicationManager.FindByIdAsync(applicationId, ct);
            if (application is null)
            {
                continue;
            }

            string? clientId = await applicationManager.GetClientIdAsync(application, ct);
            if (clientId is null)
            {
                continue;
            }

            connected.Add(new ConnectedApplicationDto(
                (await authorizationManager.GetIdAsync(authorization, ct))!,
                clientId,
                await applicationManager.GetDisplayNameAsync(application, ct) ?? clientId,
                [.. await authorizationManager.GetScopesAsync(authorization, ct)],
                await authorizationManager.GetCreationDateAsync(authorization, ct)));
        }

        return connected
            .OrderByDescending(app => app.CreatedAt ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<bool> WithdrawAsync(Guid userId, string authorizationId, CancellationToken ct = default)
    {
        // The store's FindByIdAsync throws on an unparseable key rather than answering null.
        if (!Guid.TryParse(authorizationId, out _))
        {
            return false;
        }

        object? authorization = await authorizationManager.FindByIdAsync(authorizationId, ct);
        if (authorization is null
            || !string.Equals(
                await authorizationManager.GetSubjectAsync(authorization, ct),
                userId.ToString(),
                StringComparison.Ordinal)
            || !await IsPermanentConsentAsync(authorization, ct))
        {
            return false;
        }

        await authorizationManager.TryRevokeAsync(authorization, ct);
        long revokedTokens = await tokenManager.RevokeByAuthorizationIdAsync(authorizationId, ct);

        LogConsentWithdrawn(userId, authorizationId, revokedTokens);
        return true;
    }

    private async Task<bool> IsPermanentConsentAsync(object authorization, CancellationToken ct)
    {
        return string.Equals(
                await authorizationManager.GetStatusAsync(authorization, ct),
                Statuses.Valid,
                StringComparison.Ordinal)
            && string.Equals(
                await authorizationManager.GetTypeAsync(authorization, ct),
                AuthorizationTypes.Permanent,
                StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "User {UserId} withdrew consent {AuthorizationId}; revoked {RevokedTokens} tokens")]
    private partial void LogConsentWithdrawn(Guid userId, string authorizationId, long revokedTokens);
}
