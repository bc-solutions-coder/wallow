using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class InvitationService(
    IInvitationRepository invitationRepository,
    IMessageBus messageBus,
    ITenantContext tenantContext,
    TimeProvider timeProvider,
    IdentityDbContext dbContext) : IInvitationService
{
    public async Task<Invitation> CreateInvitationAsync(Guid tenantId, string email, Guid createdByUserId, CancellationToken ct = default)
    {
        DateTimeOffset expiresAt = timeProvider.GetUtcNow().AddDays(7);

        Invitation invitation = Invitation.Create(
            tenantContext.TenantId,
            email,
            expiresAt,
            createdByUserId,
            timeProvider);

        invitationRepository.Add(invitation);
        await invitationRepository.SaveChangesAsync(ct);

        await messageBus.PublishAsync(new InvitationCreatedEvent
        {
            InvitationId = invitation.Id.Value,
            TenantId = tenantContext.TenantId.Value,
            Email = email,
            Token = invitation.Token,
            ExpiresAt = expiresAt
        });

        return invitation;
    }

    public async Task RevokeInvitationAsync(Guid invitationId, Guid actorId, CancellationToken ct = default)
    {
        InvitationId id = InvitationId.Create(invitationId);
        Invitation invitation = await invitationRepository.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException("Invitation", invitationId);

        invitation.Revoke(actorId, timeProvider);
        await invitationRepository.SaveChangesAsync(ct);
    }

    public Task<Invitation?> GetInvitationByTokenAsync(string token, CancellationToken ct = default)
    {
        return invitationRepository.GetByTokenAsync(token, ct);
    }

    public async Task AcceptInvitationAsync(string token, Guid userId, CancellationToken ct = default)
    {
        Invitation invitation = await invitationRepository.GetByTokenAsync(token, ct)
            ?? throw new EntityNotFoundException("Invitation", token);

        invitation.Accept(userId, timeProvider);
        await invitationRepository.SaveChangesAsync(ct);
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();

        List<Invitation> expiredInvitations = await dbContext.Invitations
            .AsTracking()
            .Where(i => i.Status == InvitationStatus.Pending && i.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (Invitation invitation in expiredInvitations)
        {
            invitation.MarkExpired();
        }

        if (expiredInvitations.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
