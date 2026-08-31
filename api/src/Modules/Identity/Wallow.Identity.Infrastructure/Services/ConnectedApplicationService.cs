using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Extensions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// The consent ledger a user can read and edit. Connected applications are the user's Valid
/// permanent authorizations — the durable consent records — never the per-login ad-hoc rows
/// sign-ins leave behind. Tokens chain to those ad-hoc rows, not to the consent record, so
/// withdrawing consent revokes the permanent record and then walks the user's tokens for that
/// application (and the ad-hoc rows they chain to): refresh dies with <c>invalid_grant</c> and
/// token-entry validation refuses the surviving access tokens on their next request.
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

        long revokedTokens = 0;
        string? applicationId = await authorizationManager.GetApplicationIdAsync(authorization, ct);
        if (applicationId is not null)
        {
            string subject = userId.ToString();

            await foreach (object token in tokenManager.FindBySubjectAsync(subject, ct))
            {
                if (string.Equals(
                        await tokenManager.GetApplicationIdAsync(token, ct),
                        applicationId,
                        StringComparison.Ordinal)
                    && await tokenManager.TryRevokeAsync(token, ct))
                {
                    revokedTokens++;
                }
            }

            // The per-login ad-hoc rows the tokens chained to die with them, so nothing Valid
            // is left pointing at the application.
            await foreach (object adHoc in authorizationManager.FindBySubjectAsync(subject, ct))
            {
                if (string.Equals(
                        await authorizationManager.GetApplicationIdAsync(adHoc, ct),
                        applicationId,
                        StringComparison.Ordinal)
                    && (await authorizationManager.GetTypeAsync(adHoc, ct)).IsAdHocAuthorizationType())
                {
                    await authorizationManager.TryRevokeAsync(adHoc, ct);
                }
            }
        }

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
