namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Tracks client participation by SSO sid and resolves front- and back-channel logout recipients.
/// </summary>
public interface ISsoClientSessionService
{
    /// <summary>
    /// Records session participation unless the pair already exists. The implementation
    /// swallows save failures, so successful return does not guarantee persistence.
    /// </summary>
    Task RecordAsync(string sid, string clientId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Builds one iframe URL per participating client that registered a front-channel logout URI:
    /// the client's URI with <c>iss</c> (the issuer) and <c>sid</c> appended, per the OIDC
    /// front-channel logout spec.
    /// </summary>
    Task<IReadOnlyList<Uri>> BuildLogoutNotificationUrisAsync(string sid, Uri issuer, CancellationToken ct);

    /// <summary>
    /// The participating clients that registered a back-channel logout URI — the audience list
    /// the back-channel notifier mints one logout token per entry for.
    /// </summary>
    Task<IReadOnlyList<BackchannelLogoutRecipient>> ListBackchannelRecipientsAsync(string sid, CancellationToken ct);

    /// <summary>Deletes every participation row for <paramref name="sid"/>.</summary>
    Task ForgetAsync(string sid, CancellationToken ct);
}

/// <summary>
/// One back-channel logout delivery: the client id (the logout token's <c>aud</c>) and the URI
/// the token is POSTed to.
/// </summary>
public sealed record BackchannelLogoutRecipient(string ClientId, Uri LogoutUri);
