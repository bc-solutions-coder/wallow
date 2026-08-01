using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface IInvitationService
{
    /// <summary>
    /// Invites an email address into the CALLER's organization. There is deliberately no tenant
    /// parameter: a holder of <c>OrganizationsManageMembers</c> holds it in one organization, and a
    /// tenant argument would be either ignored or an invitation into somebody else's org.
    /// </summary>
    Task<Invitation> CreateInvitationAsync(string email, Guid createdByUserId, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid invitationId, Guid actorId, CancellationToken ct = default);
    Task<Invitation?> GetInvitationByTokenAsync(string token, CancellationToken ct = default);
    Task AcceptInvitationAsync(string token, Guid userId, CancellationToken ct = default);
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
