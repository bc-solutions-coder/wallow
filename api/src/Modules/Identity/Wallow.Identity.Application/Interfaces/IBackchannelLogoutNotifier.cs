namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Sends signed logout tokens to session participants with a back-channel endpoint.
/// Individual delivery failures are logged; recipient lookup and setup failures can
/// propagate. Callers must handle those failures if sign-out must continue.
/// </summary>
public interface IBackchannelLogoutNotifier
{
    /// <summary>
    /// Uses sid and userId to identify the ended session, and issuer as the token's iss claim.
    /// </summary>
    Task NotifyAsync(string sid, Guid userId, Uri issuer, CancellationToken ct);
}
