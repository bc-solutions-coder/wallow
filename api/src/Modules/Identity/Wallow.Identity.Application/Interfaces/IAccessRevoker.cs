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
    /// Ends every token a client was issued, whoever holds it, and hangs up every realtime
    /// connection opened with one. Returns how many token entries it ended.
    /// </summary>
    Task<int> RevokeClientAsync(string clientId, CancellationToken ct = default);
}
