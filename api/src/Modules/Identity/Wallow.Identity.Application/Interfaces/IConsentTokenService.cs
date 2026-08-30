namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Why a consent token was, or was not, accepted. Only <see cref="Redeemed"/> lets a consent
/// decision through; the rest name the reason for the audit trail and are otherwise all handled
/// alike, by asking again.
/// </summary>
public enum ConsentTokenOutcome
{
    /// <summary>The token was minted for this user and request and had not been used before.</summary>
    Redeemed,

    /// <summary>The decision carried no token at all.</summary>
    Missing,

    /// <summary>The token was not minted by this server, or has expired.</summary>
    Invalid,

    /// <summary>The token was minted for another user or another authorize request.</summary>
    Mismatched,

    /// <summary>The token had already been redeemed.</summary>
    Replayed,
}

/// <summary>
/// Mints and redeems the single-use token a consent decision must carry. The token binds the
/// decision to the signed-in user and to one pending authorize request, so a decision cannot be
/// forged onto a link, replayed, or carried from one request or user to another.
/// </summary>
public interface IConsentTokenService
{
    /// <summary>Mints a token for <paramref name="userId"/>'s pending request.</summary>
    /// <param name="userId">The subject the consent screen is shown to.</param>
    /// <param name="requestFingerprint">
    /// An opaque digest of the pending authorize request; the same request must digest the same way
    /// when the decision comes back.
    /// </param>
    string Issue(string userId, string requestFingerprint);

    /// <summary>
    /// Redeems a token once. A second redemption of the same token is <see cref="ConsentTokenOutcome.Replayed"/>
    /// even when everything else about it still matches.
    /// </summary>
    ValueTask<ConsentTokenOutcome> RedeemAsync(
        string? token,
        string userId,
        string requestFingerprint,
        CancellationToken ct);
}
