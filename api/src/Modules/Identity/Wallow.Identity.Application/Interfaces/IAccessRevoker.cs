namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Revokes OpenIddict credentials and requests realtime disconnection at the relevant
/// user, organization, client, or session scope. Consent withdrawal is handled separately.
/// </summary>
public interface IAccessRevoker
{
    /// <summary>
    /// Revokes the user's organization-stamped authorizations and tokens for clients bound
    /// to this organization, then requests realtime disconnection within that organization.
    /// </summary>
    Task RevokeMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Revokes authorizations stamped with this user's sid and their tokens.
    /// Does not revoke the user's other sessions or disconnect realtime streams here.
    /// </summary>
    Task RevokeSessionAsync(Guid userId, string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Revokes the user's tokens and ad-hoc authorizations, preserving permanent consent.
    /// Requests realtime disconnection in each active membership's organization.
    /// </summary>
    Task RevokeUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Attempts to revoke the client's tokens, then requests realtime disconnection.
    /// Returns the successful revocation count, or zero when no application is found.
    /// </summary>
    Task<int> RevokeClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>
    /// Revokes credentials for registered clients bound to the organization and for each
    /// member in it. Does not change client lifecycle status or restore revoked credentials.
    /// </summary>
    Task RevokeOrganizationAsync(Guid organizationId, CancellationToken ct = default);
}
