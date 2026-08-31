namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Delivers OIDC back-channel logout: one signed logout token POSTed to every relying party that
/// participates in the ending SSO session and registered a back-channel logout URI. Best-effort by
/// contract — delivery failures are logged and never surface to the caller, because an
/// unreachable relying party must not block the user's own sign-out.
/// </summary>
/// <remarks>
/// Deliberately keyed by <c>sid</c> rather than by an HTTP request so later triggers (admin
/// session revocation, user deactivation) can notify the same way end-session does.
/// </remarks>
public interface IBackchannelLogoutNotifier
{
    /// <summary>
    /// Notifies every participating relying party that session <paramref name="sid"/> of user
    /// <paramref name="userId"/> ended. <paramref name="issuer"/> becomes the token's <c>iss</c>,
    /// which relying parties validate against the discovery document before ending anything.
    /// </summary>
    Task NotifyAsync(string sid, Guid userId, Uri issuer, CancellationToken ct);
}
