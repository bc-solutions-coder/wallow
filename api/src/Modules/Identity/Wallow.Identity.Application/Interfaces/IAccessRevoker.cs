namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// The one place access already granted is taken back: the tokens OpenIddict issued and the
/// realtime connections those tokens opened. Two ways of naming what to revoke — a person's
/// standing in one organization, or a client whatever its holders — and one mechanism behind
/// both, so a membership revocation, a secret rotation and a client suspension cannot drift apart
/// in what "revoked" means. Consents are never touched: a person signs back in without being
/// asked again.
/// </summary>
public interface IAccessRevoker
{
    /// <summary>
    /// Ends every credential a person still holds against one organization once their membership
    /// stops being active. Scoped to the organization on purpose: somebody suspended in one
    /// organization keeps whatever access their other memberships earn them.
    /// </summary>
    Task RevokeMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Ends one browser session at end-session: revokes every authorization stamped with the
    /// session's <c>sid</c> and all tokens chained to them. Scoped to that one session — the
    /// user's other sessions keep refreshing, and the consent records sign-in reads stay put.
    /// </summary>
    Task RevokeSessionAsync(Guid userId, string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Ends every session a person holds anywhere, the moment their account is deactivated:
    /// every token issued to them and every per-login authorization is revoked, and their live
    /// realtime streams are hung up in each organization they were active in. Consent records
    /// stay put, so a reactivated account signs back in without being asked again.
    /// </summary>
    Task RevokeUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ends every token a client was issued, whoever holds it, and hangs up every realtime
    /// connection opened with one. Returns how many token entries it ended.
    /// </summary>
    Task<int> RevokeClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Takes back everything an organization's closure implies: every bound client's tokens,
    /// whoever holds them, and every member's credentials against the organization, live realtime
    /// streams included. Archive and platform suspension both mean this; individual client
    /// suspensions are untouched, so lifting the closure restores exactly the clients the
    /// organization did not suspend itself.
    /// </summary>
    Task RevokeOrganizationAsync(Guid organizationId, CancellationToken ct = default);
}
