using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface IInvitationService
{
    /// <summary>
    /// Creates or renews an invitation in the caller's resolved tenant.
    /// The caller must authorize membership management in that organization.
    /// </summary>
    Task<Invitation> CreateInvitationAsync(string email, Guid createdByUserId, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid invitationId, Guid actorId, CancellationToken ct = default);
    Task<Invitation?> GetInvitationByTokenAsync(string token, CancellationToken ct = default);
    Task AcceptInvitationAsync(string token, Guid userId, CancellationToken ct = default);
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
