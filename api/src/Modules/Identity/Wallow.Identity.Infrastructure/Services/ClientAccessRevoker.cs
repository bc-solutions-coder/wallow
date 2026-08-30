using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Every token OpenIddict stores names the application it was issued to, so revoking by client is
/// one walk over that index. Access tokens fail on their next bearer call through token-entry
/// validation, refresh tokens fail at the token endpoint with <c>invalid_grant</c>.
/// </summary>
public sealed partial class ClientAccessRevoker(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictApplicationManager applicationManager,
    ILogger<ClientAccessRevoker> logger) : IClientAccessRevoker
{
    public async Task<int> RevokeAsync(string clientId, CancellationToken ct = default)
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

        LogTokensRevoked(clientId, revoked);
        return revoked;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Revoked {RevokedTokenCount} tokens of client {ClientId}")]
    private partial void LogTokensRevoked(string clientId, int revokedTokenCount);
}
