namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Ends every token a client was issued, whoever holds it: the by-application counterpart of
/// <see cref="IMembershipAccessRevoker"/>. Consents are kept, so a person signs back in without
/// being asked again.
/// </summary>
public interface IClientAccessRevoker
{
    /// <summary>Revokes every token entry of the named client and returns how many it ended.</summary>
    Task<int> RevokeAsync(string clientId, CancellationToken ct = default);
}
