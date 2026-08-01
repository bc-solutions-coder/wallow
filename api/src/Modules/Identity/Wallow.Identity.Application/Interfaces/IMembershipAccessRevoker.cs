namespace Wallow.Identity.Application.Interfaces;

/// <summary>
/// Ends every credential a person still holds against one organization once their membership
/// stops being active: the tokens already issued to them there, and the realtime connections
/// those tokens opened.
///
/// Scoped to the organization on purpose. Somebody suspended in one organization keeps whatever
/// access their other memberships earn them.
/// </summary>
public interface IMembershipAccessRevoker
{
    Task RevokeAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}
