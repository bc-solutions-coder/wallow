namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Consent-token validation result. Only <see cref="Redeemed"/> permits the decision.
/// </summary>
public enum ConsentTokenOutcome
{
    /// <summary>
    /// The token matched the user/request and created a redemption-cache entry.
    /// </summary>
    Redeemed,

    /// <summary>The decision carried no token at all.</summary>
    Missing,

    /// <summary>
    /// The token could not be unprotected or decoded, or has expired.
    /// </summary>
    Invalid,

    /// <summary>The token was minted for another user or another authorize request.</summary>
    Mismatched,

    /// <summary>
    /// A redemption entry was already visible in the cache.
    /// </summary>
    Replayed,
}

/// <summary>
/// Binds consent decisions to a user and request fingerprint. Replay detection uses
/// a redemption cache and depends on that cache retaining and coordinating entries.
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
    /// Checks the protected user/request binding and records redemption in the cache.
    /// Returns Replayed when the redemption factory does not run because an entry exists.
    /// </summary>
    ValueTask<ConsentTokenOutcome> RedeemAsync(
        string? token,
        string userId,
        string requestFingerprint,
        CancellationToken ct);
}
