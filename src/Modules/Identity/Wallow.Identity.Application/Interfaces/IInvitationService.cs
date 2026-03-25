using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface IInvitationService
{
    Task<Invitation> CreateInvitationAsync(Guid tenantId, string email, Guid createdByUserId, CancellationToken ct = default);
    Task RevokeInvitationAsync(Guid invitationId, Guid actorId, CancellationToken ct = default);
    Task<Invitation?> GetInvitationByTokenAsync(string token, CancellationToken ct = default);
    Task AcceptInvitationAsync(string token, Guid userId, CancellationToken ct = default);
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
